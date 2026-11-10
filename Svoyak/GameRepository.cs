using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;

namespace Svoyak;

/// <summary>Loads and saves complete game templates in the existing database.</summary>
public sealed class GameRepository
{
    public List<Game> LoadGames()
    {
        using var database = new QuizDb();
        return database.Games.AsNoTracking()
            .Include(game => game.Topics.OrderBy(topic => topic.Id))
            .ThenInclude(topic => topic.Questions.OrderBy(question => question.Id))
            .OrderBy(game => game.Title).ToList();
    }

    public void Save(Game game)
    {
        using var database = new QuizDb();
        using var transaction = database.Database.BeginTransaction();
        if (game.Id != 0)
        {
            var stored = database.Games.Include(item => item.Topics)
                .ThenInclude(topic => topic.Questions).Single(item => item.Id == game.Id);
            stored.Title = game.Title.Trim();
            stored.UpdatedAt = DateTime.UtcNow.ToString("O");
            database.Topics.RemoveRange(stored.Topics);
            database.SaveChanges();
            stored.Topics = game.Topics;
        }
        else
        {
            database.Games.Add(game);
        }

        // The editor works on a detached graph; question IDs are reassigned on save.
        foreach (var topic in game.Topics)
        {
            topic.Id = 0;
            topic.GameId = game.Id;
            foreach (var question in topic.Questions)
            {
                question.Id = 0;
                question.TopicId = 0;
            }
        }
        database.SaveChanges();
        transaction.Commit();
    }

    public void Delete(int gameId)
    {
        using var database = new QuizDb();
        var game = database.Games.Include(item => item.Topics)
            .ThenInclude(topic => topic.Questions).Single(item => item.Id == gameId);
        database.Games.Remove(game);
        database.SaveChanges();
    }

    public static Game Copy(Game source, bool keepId = true) => new()
    {
        Id = keepId ? source.Id : 0,
        Title = source.Title,
        Description = source.Description,
        Topics = source.Topics.Select(topic => new Topic
        {
            Title = topic.Title,
            Questions = topic.Questions.Select(question => new Question
            {
                Text = question.Text,
                Answer = question.Answer,
                Points = question.Points,
                MediaPath = question.MediaPath,
                MediaType = question.MediaType
            }).ToList()
        }).ToList()
    };
}

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
