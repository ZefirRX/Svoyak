using Microsoft.EntityFrameworkCore;

namespace Svoyak;

/// <summary>Loads and saves complete game templates in the existing database.</summary>
public sealed class GameRepository(string? databasePath = null)
{
    public List<Game> LoadGames()
    {
        using var database = new QuizDb(databasePath);
        return database.Games.AsNoTracking()
            .Include(game => game.Topics.OrderBy(topic => topic.Id))
            .ThenInclude(topic => topic.Questions.OrderBy(question => question.Id))
            .OrderBy(game => game.Title).ToList();
    }

    public void Save(Game game)
    {
        using var database = new QuizDb(databasePath);
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
        using var database = new QuizDb(databasePath);
        var game = database.Games.Include(item => item.Topics)
            .ThenInclude(topic => topic.Questions).Single(item => item.Id == gameId);
        database.Games.Remove(game);
        database.SaveChanges();
    }

    internal void Import(IReadOnlyList<Game> games, IReadOnlyDictionary<Question, int> durations, TimerSettings timers)
    {
        if (games.Count == 0)
            return;

        using var database = new QuizDb(databasePath);
        using var transaction = database.Database.BeginTransaction();
        database.Games.AddRange(games);
        database.SaveChanges();

        var importedTimers = new Dictionary<string, int>();
        foreach (var game in games)
        {
            for (int topicIndex = 0; topicIndex < game.Topics.Count; topicIndex++)
            {
                var questions = game.Topics[topicIndex].Questions;
                for (int questionIndex = 0; questionIndex < questions.Count; questionIndex++)
                {
                    if (durations.TryGetValue(questions[questionIndex], out int seconds))
                        importedTimers[TimerSettings.Key(game.Id, topicIndex, questionIndex)] = seconds;
                }
            }
        }

        timers.Import(importedTimers, transaction.Commit);
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
