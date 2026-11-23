using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Svoyak;

public partial class MainWindow
{
    private void ReturnToBoard(object sender, RoutedEventArgs e)
    {
        if (session.Game is not { } game) return;
        session.CloseQuestion();
        ShowView(BoardView, "Режим: игра");
        RenderBoard(game);
        RenderScores();
        RefreshProjector();
    }

    private void RenderBoard(Game game)
    {
        BoardTitle.Text = $"{game.Title}   Сыграно {session.PlayedQuestions.Count} из {game.Topics.Sum(topic => topic.Questions.Count)}";
        QuestionBoard.Reset(BoardCells, game.Topics, headerHeight: 44, questionHeight: 60);
        for (int column = 0; column < game.Topics.Count; column++)
        {
            var topic = game.Topics[column];
            var header = new Border
            {
                Background = (Brush)FindResource("AccentBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 5, 5),
                Child = new TextBlock
                {
                    Text = topic.Title,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            QuestionBoard.AddCell(BoardCells, header, column, 0);
            for (int row = 0; row < topic.Questions.Count; row++)
            {
                var question = topic.Questions[row];
                var button = ActionButton(question.Points.ToString(), () => OpenQuestion(topic, question));
                button.Margin = new Thickness(0, 0, 5, 5);
                button.VerticalAlignment = VerticalAlignment.Stretch;
                button.FontSize = 26;
                button.FontWeight = FontWeights.Bold;
                button.Foreground = (Brush)FindResource("AccentBrush");
                button.IsEnabled = !session.PlayedQuestions.Contains(question.Id);
                QuestionBoard.AddCell(BoardCells, button, column, row + 1);
            }
        }
    }

    private void RenderScores()
    {
        ScoreRows.Children.Clear();
        foreach (var team in teams)
        {
            var score = new TextBox
            {
                Text = team.Score.ToString(),
                MinWidth = 116,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("AccentBrush"),
                TextAlignment = TextAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(score, $"Счёт команды «{team.Name}»");
            score.LostKeyboardFocus += (_, _) => CommitScore(team, score);
            score.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitScore(team, score);
                    score.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    score.Text = team.Score.ToString();
                    score.SelectAll();
                    e.Handled = true;
                }
            };

            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            DockPanel.SetDock(score, Dock.Right);
            score.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(score);
            var name = Text(team.Name);
            name.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name);
            ScoreRows.Children.Add(row);
        }
        UpdateScoreHistory();
    }

    private void UpdateScoreHistory()
    {
        var action = session.LastAction;
        string summary = action is null ? "Действий пока нет" : "Последнее: " + action;
        if (LastAction.Text != summary && BoardView.IsVisible) Motion.Enter(LastAction, 4, 180);
        LastAction.Text = summary;
        UndoButton.IsEnabled = action is not null;
    }

    private void CommitScore(Team team, TextBox input)
    {
        bool changed = int.TryParse(input.Text, out int score) && session.SetScore(team, score);
        input.Text = team.Score.ToString();
        if (changed)
        {
            UpdateScoreHistory();
            RefreshProjector();
            Motion.Pulse(input);
        }
    }

    private void UndoScore(object sender, RoutedEventArgs e)
    {
        if (session.Undo()) ReturnToBoard(sender, e);
    }

    private void FinishGame(object sender, RoutedEventArgs e)
    {
        ShowView(ResultsView, "Режим: игра");
        ResultsTitle.Text = $"ИТОГИ ИГРЫ «{session.Game?.Title.ToUpperInvariant()}»";
        ResultRows.Children.Clear();
        int place = 1;
        foreach (var team in teams.OrderByDescending(team => team.Score))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rank = Text((place++).ToString(), 16);
            var name = Text(team.Name, 17);
            var score = Text(team.Score.ToString(), 20);
            score.FontWeight = FontWeights.Bold;
            score.Foreground = (Brush)FindResource("AccentBrush");
            Grid.SetColumn(name, 1);
            Grid.SetColumn(score, 2);
            row.Children.Add(rank);
            row.Children.Add(name);
            row.Children.Add(score);
            ResultRows.Children.Add(new Border
            {
                Style = (Style)FindResource("Card"),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 5),
                Child = row
            });
        }
    }
}
