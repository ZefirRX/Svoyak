using System.IO;
using System.Text.Json;

namespace Svoyak;

/// <summary>The source schema has no timer column. Store durations by game/topic/question position.</summary>
public sealed class TimerSettings
{
    private readonly string filePath = Path.Combine(AppContext.BaseDirectory, "timers.json");
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

    public void Save() => File.WriteAllText(filePath, JsonSerializer.Serialize(durations));
}
