using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Svoyak;

/// <summary>Displays the board or current question without revealing the answer.</summary>
public partial class ProjectorWindow : Window
{
    private string? displayedImagePath;

    public ProjectorWindow() => InitializeComponent();

    public void Update(string gameTitle, string topicTitle, Question? question, int seconds, IReadOnlyList<Team> teams, Game? game = null, IReadOnlySet<int>? playedQuestions = null)
    {
        Caption.Text = question == null ? gameTitle : $"{topicTitle} · {question.Points} баллов";
        Prompt.Text = question?.Text ?? "Выберите вопрос на табло";
        Countdown.Text = question == null ? "" : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        UpdateImage(question);
        bool showBoard = question == null && game != null;
        BoardView.Visibility = showBoard ? Visibility.Visible : Visibility.Collapsed;
        QuestionView.Visibility = showBoard ? Visibility.Collapsed : Visibility.Visible;
        if (showBoard) RenderBoard(game!, playedQuestions);
        Scores.Children.Clear();
        foreach (var team in teams)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = team.Name, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = team.Score.ToString(), FontSize = 28, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            Scores.Children.Add(panel);
        }
    }

    private void RenderBoard(Game game, IReadOnlySet<int>? playedQuestions)
    {
        AudienceBoard.Children.Clear();
        AudienceBoard.ColumnDefinitions.Clear();
        AudienceBoard.RowDefinitions.Clear();
        if (game.Topics.Count == 0) return;

        int questionRows = game.Topics.Max(topic => topic.Questions.Count);
        foreach (var topic in game.Topics)
            AudienceBoard.ColumnDefinitions.Add(new ColumnDefinition());
        AudienceBoard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        for (int row = 0; row < questionRows; row++)
            AudienceBoard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(88) });

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
            TextAlignment = TextAlignment.Center,
            TextDecorations = isPlayed ? TextDecorations.Strikethrough : null
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
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        AudienceBoard.Children.Add(cell);
    }

    private void UpdateImage(Question? question)
    {
        string? imagePath = question?.MediaType == 1 ? question.MediaPath : null;
        if (imagePath == displayedImagePath) return;
        displayedImagePath = imagePath;
        QuestionPicture.Source = null;
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            try { QuestionPicture.Source = QuestionImage.Load(imagePath); }
            catch (Exception) { /* Keep the question visible if the image is unavailable. */ }
        }
        bool hasImage = QuestionPicture.Source != null;
        PromptRow.Height = hasImage ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        PictureRow.Height = hasImage ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Prompt.FontSize = hasImage ? 36 : 56;
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
