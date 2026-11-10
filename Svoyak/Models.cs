namespace Svoyak;

// Entities correspond to the existing SQLite tables (Database First).
public class Game
{
    public int Id { get; set; }
    public string Title { get; set; } = "Новая игра";
    public string? Description { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<Topic> Topics { get; set; } = [];
}

public class Topic
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Title { get; set; } = "Новая тема";
    public Game Game { get; set; } = null!;
    public List<Question> Questions { get; set; } = [];
}

public class Question
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public string Text { get; set; } = "";
    public string Answer { get; set; } = "";
    public int Points { get; set; } = 100;
    public string? MediaPath { get; set; }
    public int MediaType { get; set; }
    public Topic Topic { get; set; } = null!;
}

public class Team
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
}
