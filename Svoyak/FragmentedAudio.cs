using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Svoyak;

// Rebuilds the sample tables of a single AAC track without decoding its frames.
internal static class FragmentedAudio
{
    private const uint BaseDataOffsetPresent = 0x1;
    private const uint SampleDescriptionPresent = 0x2;
    private const uint DefaultDurationPresent = 0x8;
    private const uint DefaultSizePresent = 0x10;
    private const uint DurationIsEmpty = 0x010000;

    private const uint DataOffsetPresent = 0x1;
    private const uint FirstSampleFlagsPresent = 0x4;
    private const uint SampleDurationPresent = 0x100;
    private const uint SampleSizePresent = 0x200;
    private const uint SampleFlagsPresent = 0x400;
    private const uint CompositionOffsetPresent = 0x800;

    private readonly record struct Box(string Type, int Offset, ReadOnlyMemory<byte> Data);
    private sealed record AudioSamples(List<uint> Sizes, List<(uint Count, uint Duration)> Timing,
        List<ReadOnlyMemory<byte>> Runs);

    public static void WriteM4a(byte[] source, Stream output)
    {
        var boxes = ReadBoxes(source).ToArray();
        var fragments = boxes.Where(box => box.Type == "moof").ToArray();
        if (fragments.Length == 0)
        {
            output.Write(source);
            return;
        }

        var movie = Children(boxes.Single(box => box.Type == "moov"));
        ValidateAudioTrack(movie);
        var samples = ReadSamples(source, movie, fragments);

        WriteBox(output, "ftyp", "M4A \0\0\0\0isomiso2mp41"u8);
        WriteNumber(output, checked((uint)(8 + samples.Runs.Sum(run => (long)run.Length))));
        output.Write("mdat"u8);
        uint chunkOffset = checked((uint)output.Position);
        foreach (var run in samples.Runs) output.Write(run.Span);

        var tables = new Dictionary<string, byte[]>
        {
            ["stts"] = Numbers(new uint[] { 0, (uint)samples.Timing.Count }
                .Concat(samples.Timing.SelectMany(entry => new[] { entry.Count, entry.Duration }))),
            ["stsc"] = Numbers([0, 1, 1, (uint)samples.Sizes.Count, 1]),
            ["stsz"] = Numbers(new uint[] { 0, 0, (uint)samples.Sizes.Count }.Concat(samples.Sizes)),
            ["stco"] = Numbers([0, 1, chunkOffset])
        };
        WriteBox(output, "moov", RewriteMovie(movie, tables));
    }

    private static void ValidateAudioTrack(Box[] movie)
    {
        var track = Children(movie.Single(box => box.Type == "trak"));
        var media = Children(track.Single(box => box.Type == "mdia"));
        Require(media.Single(box => box.Type == "hdlr").Data.Span.Slice(8, 4).SequenceEqual("soun"u8));
        var mediaInfo = Children(media.Single(box => box.Type == "minf"));
        var samples = Children(mediaInfo.Single(box => box.Type == "stbl"));
        var description = samples.Single(box => box.Type == "stsd").Data.Span;
        Require(Read(description, 4) == 1 && description.Slice(12, 4).SequenceEqual("mp4a"u8));
    }

    private static AudioSamples ReadSamples(byte[] source, Box[] movie, Box[] fragments)
    {
        var defaults = Children(movie.Single(box => box.Type == "mvex")).Single(box => box.Type == "trex").Data.Span;
        var trackId = Read(defaults, 4);
        var sizes = new List<uint>();
        var timing = new List<(uint Count, uint Duration)>();
        var audio = new List<ReadOnlyMemory<byte>>();
        foreach (var fragment in fragments)
        {
            var parts = Children(Children(fragment).Single(box => box.Type == "traf"));
            var header = parts.Single(box => box.Type == "tfhd").Data.Span;
            var flags = Read(header, 0);
            Require(Read(header, 4) == trackId && (flags & DurationIsEmpty) == 0);
            int field = 8;
            // With one track fragment, an omitted base offset implies the start of moof.
            long offsetBase = fragment.Offset;
            if ((flags & BaseDataOffsetPresent) != 0)
            {
                offsetBase = checked((long)BinaryPrimitives.ReadUInt64BigEndian(header[field..]));
                field += 8;
            }
            Require(Optional(header, ref field, flags, SampleDescriptionPresent, Read(defaults, 8)) == 1);
            uint duration = Optional(header, ref field, flags, DefaultDurationPresent, Read(defaults, 12));
            uint size = Optional(header, ref field, flags, DefaultSizePresent, Read(defaults, 16));

            long position = offsetBase;
            foreach (var run in parts.Where(box => box.Type == "trun"))
            {
                var data = run.Data.Span;
                flags = Read(data, 0) & 0xffffff;
                uint count = Read(data, 4);
                field = 8;
                if ((flags & DataOffsetPresent) != 0)
                    position = offsetBase + unchecked((int)Optional(data, ref field, flags, DataOffsetPresent, 0));
                Optional(data, ref field, flags, FirstSampleFlagsPresent, 0);
                Require(count <= source.Length && (flags & CompositionOffsetPresent) == 0);
                int runStart = checked((int)position);
                for (uint index = 0; index < count; index++)
                {
                    uint sampleDuration = Optional(data, ref field, flags, SampleDurationPresent, duration);
                    uint sampleSize = Optional(data, ref field, flags, SampleSizePresent, size);
                    Optional(data, ref field, flags, SampleFlagsPresent, 0);
                    Require(sampleDuration > 0 && sampleSize > 0);
                    position += sampleSize;
                    sizes.Add(sampleSize);
                    if (timing.Count > 0 && timing[^1].Duration == sampleDuration)
                        timing[^1] = (timing[^1].Count + 1, sampleDuration);
                    else
                        timing.Add((1, sampleDuration));
                }
                audio.Add(source.AsMemory(runStart, checked((int)(position - runStart))));
            }
        }
        Require(sizes.Count > 0);
        return new AudioSamples(sizes, timing, audio);
    }

    private static byte[] RewriteMovie(Box[] boxes, Dictionary<string, byte[]> tables)
    {
        using var output = new MemoryStream();
        foreach (var box in boxes)
        {
            if (box.Type == "mvex") continue;
            if (box.Type is "trak" or "mdia" or "minf" or "stbl")
                WriteBox(output, box.Type, RewriteMovie(Children(box), tables));
            else if (tables.TryGetValue(box.Type, out var table))
                WriteBox(output, box.Type, table);
            else if (box.Type != "co64")
                WriteBox(output, box.Type, box.Data.Span);
            else if (!boxes.Any(child => child.Type == "stco"))
                WriteBox(output, "stco", tables["stco"]);
        }
        return output.ToArray();
    }

    private static Box[] Children(Box box) => ReadBoxes(box.Data).ToArray();

    private static IEnumerable<Box> ReadBoxes(ReadOnlyMemory<byte> data)
    {
        for (int offset = 0; offset < data.Length;)
        {
            Require(data.Length - offset >= 8);
            uint size = Read(data.Span, offset);
            int headerLength = size == 1 ? 16 : 8;
            long length = size switch
            {
                0 => data.Length - offset,
                1 => checked((long)BinaryPrimitives.ReadUInt64BigEndian(data.Span[(offset + 8)..])),
                _ => size
            };
            Require(length >= headerLength && length <= data.Length - offset);
            string type = Encoding.ASCII.GetString(data.Span.Slice(offset + 4, 4));
            yield return new Box(type, offset, data.Slice(offset + headerLength, (int)length - headerLength));
            offset += (int)length;
        }
    }

    private static uint Optional(ReadOnlySpan<byte> data, ref int position, uint flags, uint bit, uint fallback)
    {
        if ((flags & bit) == 0) return fallback;
        uint value = Read(data, position);
        position += 4;
        return value;
    }

    private static uint Read(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);

    private static byte[] Numbers(IEnumerable<uint> values)
    {
        using var output = new MemoryStream();
        foreach (uint value in values) WriteNumber(output, value);
        return output.ToArray();
    }

    private static void WriteNumber(Stream output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        output.Write(bytes);
    }

    private static void WriteBox(Stream output, string type, ReadOnlySpan<byte> data)
    {
        WriteNumber(output, checked((uint)data.Length + 8));
        output.Write(Encoding.ASCII.GetBytes(type));
        output.Write(data);
    }

    private static void Require(bool valid)
    {
        if (!valid) throw new InvalidDataException("Неподдерживаемая структура MP4/AAC.");
    }
}
