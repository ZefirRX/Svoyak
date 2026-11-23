namespace Svoyak;

internal sealed class GameSession
{
    private readonly HashSet<int> playedQuestions = [];
    private readonly HashSet<(int QuestionId, Team Team)> penalties = [];
    private readonly Stack<ScoreChange> history = [];

    public Game? Game { get; private set; }
    public Topic? Topic { get; private set; }
    public Question? Question { get; private set; }
    public IReadOnlySet<int> PlayedQuestions => playedQuestions;
    public string? LastAction => history.TryPeek(out var change) ? change.Description : null;

    public void Start(Game game, IReadOnlyList<Team> teams)
    {
        Game = game;
        CloseQuestion();
        playedQuestions.Clear();
        penalties.Clear();
        history.Clear();
        foreach (var team in teams)
        {
            team.Score = 0;
            if (string.IsNullOrWhiteSpace(team.Name)) team.Name = "Команда";
        }
    }

    public void OpenQuestion(Topic topic, Question question)
    {
        Topic = topic;
        Question = question;
    }

    public void CloseQuestion()
    {
        Topic = null;
        Question = null;
    }

    public bool HasPenalty(Team team, Question question) => penalties.Contains((question.Id, team));

    public bool Penalize(Team team, Question question)
    {
        if (!penalties.Add((question.Id, team))) return false;

        team.Score -= question.Points;
        history.Push(new ScoreChange(team, -(long)question.Points,
            $"{team.Name} −{question.Points} (неверный ответ)", question.Id, IsPenalty: true));
        return true;
    }

    public Question? CompleteQuestion(Team? team)
    {
        if (Question is not { } question) return null;

        int points = team is null ? 0 : question.Points;
        if (team is not null) team.Score += points;
        playedQuestions.Add(question.Id);
        string description = team is null ? "Вопрос без ответа" : $"{team.Name} +{points} ({Topic?.Title})";
        history.Push(new ScoreChange(team, points, description, question.Id));
        CloseQuestion();
        return question;
    }

    public bool SetScore(Team team, int score)
    {
        if (team.Score == score) return false;
        long difference = (long)score - team.Score;
        team.Score = score;
        history.Push(new ScoreChange(team, difference, $"{team.Name} {difference:+0;-0} (вручную)"));
        return true;
    }

    public bool Undo()
    {
        if (!history.TryPop(out var change)) return false;

        if (change.Team is { } team) team.Score = (int)(team.Score - change.Points);
        if (change.QuestionId is int questionId)
        {
            if (change.IsPenalty && change.Team is { } penalizedTeam)
                penalties.Remove((questionId, penalizedTeam));
            else
                playedQuestions.Remove(questionId);
        }
        return true;
    }

    private sealed record ScoreChange(Team? Team, long Points, string Description,
        int? QuestionId = null, bool IsPenalty = false);
}
