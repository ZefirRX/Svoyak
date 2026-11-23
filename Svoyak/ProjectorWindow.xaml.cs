using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Svoyak;

/// <summary>Displays the board, the current question, and the answer to a completed question.</summary>
public partial class ProjectorWindow : Window
{
    private string? displayedImagePath;
    private Question? displayedQuestion;
    private Game? displayedGame;
    private Dictionary<Team, int> displayedScores = [];
    private readonly DispatcherTimer answerTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    public ProjectorWindow()
    {
        InitializeComponent();
        answerTimer.Tick += (_, _) =>
        {
            answerTimer.Stop();
            AnswerView.Visibility = Visibility.Collapsed;
            Countdown.Visibility = Visibility.Visible;
            AnimateCurrentView();
        };
        Closed += (_, _) => answerTimer.Stop();
    }

    internal void ShowAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return;
        AnswerText.Text = answer;
        AnswerView.Visibility = Visibility.Visible;
        Countdown.Visibility = Visibility.Hidden;
        Motion.Enter(AnswerContent, 16, 320);
        answerTimer.Stop();
        answerTimer.Start();
    }

    public void Update(string gameTitle, string topicTitle, Question? question, int seconds, IReadOnlyList<Team> teams, Game? game = null, IReadOnlySet<int>? playedQuestions = null, ImageSource? video = null)
    {
        Caption.Text = question == null ? gameTitle : $"{topicTitle} · {question.Points} баллов";
        Prompt.Text = question?.Text ?? "Выберите вопрос на табло";
        UpdateCountdown(question == null ? null : seconds);
        UpdateMedia(question, video);
        bool showBoard = question == null && game != null;
        BoardView.Visibility = showBoard ? Visibility.Visible : Visibility.Collapsed;
        QuestionView.Visibility = showBoard ? Visibility.Collapsed : Visibility.Visible;
        if (showBoard) RenderBoard(game!, playedQuestions);
        if ((question != displayedQuestion || game != displayedGame) && AnswerView.Visibility != Visibility.Visible)
            AnimateCurrentView();
        displayedQuestion = question;
        displayedGame = game;

        Scores.Children.Clear();
        var currentScores = new Dictionary<Team, int>();
        foreach (var team in teams)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = team.Name, HorizontalAlignment = HorizontalAlignment.Center });
            var score = new TextBlock { Text = team.Score.ToString(), FontSize = 28, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(score);
            if (displayedScores.TryGetValue(team, out int previous) && previous != team.Score)
                Motion.Pulse(score);
            currentScores[team] = team.Score;
            Scores.Children.Add(panel);
        }
        displayedScores = currentScores;
    }

    private void AnimateCurrentView() =>
        Motion.Enter(BoardView.Visibility == Visibility.Visible ? BoardView : QuestionView, 10, 260);

    internal void UpdateCountdown(int? seconds)
    {
        string time = seconds is int remaining ? TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss") : "";
        if (seconds is >= 0 and <= 5 && Countdown.Text != time && Countdown.Visibility == Visibility.Visible)
            Motion.Pulse(Countdown);
        Countdown.Text = time;
    }

    private void RenderBoard(Game game, IReadOnlySet<int>? playedQuestions)
    {
        QuestionBoard.Reset(AudienceBoard, game.Topics, headerHeight: 56, questionHeight: 88);

        for (int column = 0; column < game.Topics.Count; column++)
        {
            var topic = game.Topics[column];
            AddBoardCell(topic.Title, column, 0, isHeader: true);
            for (int row = 0; row < topic.Questions.Count; row++)
            {
                var question = topic.Questions[row];
                bool played = playedQuestions?.Contains(question.Id) == true;
                AddBoardCell(question.Points.ToString(), column, row + 1, isPlayed: played);
            }
        }
    }

    private void AddBoardCell(string text, int column, int row, bool isHeader = false, bool isPlayed = false)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = isHeader ? 15 : 38,
            FontWeight = FontWeights.Bold,
            Foreground = isHeader ? Brushes.White : (Brush)FindResource("AccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        var cell = new Border
        {
            Child = label,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(isHeader ? 0 : 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(127, 152, 201)),
            Background = isHeader ? (Brush)FindResource("AccentBrush")
                : new SolidColorBrush(isPlayed ? Color.FromRgb(157, 179, 221) : Color.FromRgb(207, 219, 241)),
            Opacity = isPlayed ? 0.7 : 1
        };
        QuestionBoard.AddCell(AudienceBoard, cell, column, row);
    }

    private void UpdateMedia(Question? question, ImageSource? video)
    {
        string? imagePath = question?.MediaType == QuestionMediaType.Image ? question.MediaPath : null;
        if (video != null || imagePath != displayedImagePath || QuestionPicture.Source is DrawingImage)
        {
            displayedImagePath = imagePath;
            QuestionPicture.Source = video;
            if (video == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                try { QuestionPicture.Source = QuestionImage.Load(imagePath); }
                catch (Exception) { /* Keep the question visible if the image is unavailable. */ }
            }
        }
        bool hasImage = QuestionPicture.Source != null;
        PromptRow.Height = hasImage ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PictureRow.Height = hasImage ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Prompt.FontSize = hasImage ? 36 : 56;
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
