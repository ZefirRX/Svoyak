using System.IO;
using System.Text.Json;

namespace Svoyak;

/// <summary>The source schema has no timer column. Store durations by game/topic/question position.</summary>
public sealed class TimerSettings
{
    private readonly string filePath = AppData.TimersPath;
    private Dictionary<string, int> durations = [];

    public TimerSettings()
    {
        if (!File.Exists(filePath)) return;
        try { durations = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(filePath)) ?? []; }
        catch (JsonException) { durations = []; }
    }

    public int Get(string key) => durations.GetValueOrDefault(key, 30);
    public void Set(string key, int seconds) => durations[key] = seconds;
    public void RemoveTopic(int gameId, int removedIndex)
    {
        var updated = new Dictionary<string, int>();
        foreach (var entry in durations)
        {
            var parts = entry.Key.Split(':');
            if (parts.Length != 3 || parts[0] != gameId.ToString() ||
                !int.TryParse(parts[1], out int topicIndex))
            {
                updated[entry.Key] = entry.Value;
                continue;
            }
            if (topicIndex == removedIndex) continue;
            int newIndex = topicIndex > removedIndex ? topicIndex - 1 : topicIndex;
            updated[$"{gameId}:{newIndex}:{parts[2]}"] = entry.Value;
        }
        durations = updated;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(durations));
    }

    internal static string Key(int gameId, int topicIndex, int questionIndex) =>
        $"{gameId}:{topicIndex}:{questionIndex}";

    internal void Import(Dictionary<string, int> imported, Action commit)
    {
        if (imported.Count == 0)
        {
            commit();
            return;
        }

        var combined = new Dictionary<string, int>(durations);
        foreach (var entry in imported)
            combined[entry.Key] = entry.Value;

        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        var pendingPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        var rollbackPath = pendingPath + ".bak";
        try
        {
            File.WriteAllText(pendingPath, JsonSerializer.Serialize(combined));
            bool hadTimers = File.Exists(filePath);
            if (hadTimers)
                File.Replace(pendingPath, filePath, rollbackPath);
            else
                File.Move(pendingPath, filePath);

            try { commit(); }
            catch
            {
                if (hadTimers)
                    File.Move(rollbackPath, filePath, overwrite: true);
                else
                    File.Delete(filePath);
                throw;
            }
            durations = combined;
            DeleteTemporaryFile(rollbackPath);
        }
        finally
        {
            DeleteTemporaryFile(pendingPath);
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
