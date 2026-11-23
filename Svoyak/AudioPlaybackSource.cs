using System.IO;

namespace Svoyak;

internal sealed class AudioPlaybackSource : IDisposable
{
    private string? temporaryPath;

    public string FilePath { get; }

    private AudioPlaybackSource(string path, bool temporary = false)
    {
        FilePath = path;
        temporaryPath = temporary ? path : null;
    }

    public static AudioPlaybackSource Open(string path)
    {
        if (Path.GetExtension(path).Equals(".m4a", StringComparison.OrdinalIgnoreCase))
            return new AudioPlaybackSource(path);

        using var input = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        if (input.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) != header.Length ||
            !header[4..].SequenceEqual("ftyp"u8))
            return new AudioPlaybackSource(path);

        // Downloaded AAC tracks can use an MP4 container under an .mp3 extension.
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"SvoyakAudio-{Guid.NewGuid():N}.m4a");
        var source = new AudioPlaybackSource(temporaryPath, temporary: true);
        try
        {
            input.Position = 0;
            var data = new byte[checked((int)input.Length)];
            input.ReadExactly(data);
            using var output = new FileStream(temporaryPath, FileMode.CreateNew);
            FragmentedAudio.WriteM4a(data, output);
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (temporaryPath is null) return;
        string path = temporaryPath;
        temporaryPath = null;
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
