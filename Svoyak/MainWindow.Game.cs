using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Svoyak;

public partial class MainWindow
{
    private void StartGame(object sender, RoutedEventArgs e)
    {
        if (GameList.SelectedItem is not Game game || !game.Topics.Any(topic => topic.Questions.Count > 0))
        {
            MessageBox.Show("Выберите игру с вопросами.");
            return;
        }
        session.Start(game, teams);
        ReturnToBoard(sender, e);
        OpenProjector(sender, e);
    }

    private void OpenQuestion(Topic topic, Question question)
    {
        if (session.Game is not { } game) return;
        session.OpenQuestion(topic, question);
        secondsLeft = timerSettings.Get(TimerKey(game.Id, game.Topics.IndexOf(topic), topic.Questions.IndexOf(question)));
        QuestionTitle.Text = $"{topic.Title} · {question.Points}";
        QuestionText.Text = question.Text;
        AnswerText.Text = question.Answer;
        AnswerText.Visibility = Visibility.Collapsed;
        AnswerButton.Visibility = Visibility.Visible;
        RenderAnswerButtons(question);
        RenderMedia(question);
        ShowView(QuestionView, "Режим: игра");
        UpdateTimer();
        RefreshProjector();
    }

    private void RenderAnswerButtons(Question question)
    {
        AnswerTeams.Children.Clear();
        foreach (var team in teams)
        {
            var correct = ActionButton($"Верно +{question.Points}", () => CompleteQuestion(team));
            correct.Background = new SolidColorBrush(Color.FromRgb(30, 127, 85));
            correct.Foreground = Brushes.White;
            bool alreadyWrong = session.HasPenalty(team, question);
            correct.IsEnabled = !alreadyWrong;
            var wrong = new Button
            {
                Content = alreadyWrong ? "Списано ✓" : $"Неверно −{question.Points}",
                Background = new SolidColorBrush(Color.FromRgb(166, 37, 48)),
                Foreground = Brushes.White,
                IsEnabled = !alreadyWrong
            };
            wrong.Click += (_, _) =>
            {
                RegisterWrongAnswer(team, question);
                wrong.Content = "Списано ✓";
                wrong.IsEnabled = false;
                correct.IsEnabled = false;
            };
            AnswerTeams.Children.Add(ActionRow(Text(team.Name), correct, wrong));
        }
    }

    private void RegisterWrongAnswer(Team team, Question question)
    {
        if (!session.Penalize(team, question)) return;
        RenderScores();
        RefreshProjector();
    }

    private void CompleteQuestion(Team? team)
    {
        if (session.CompleteQuestion(team) is not { } question) return;
        projector?.ShowAnswer(question.Answer);
        ReturnToBoard(this, new RoutedEventArgs());
    }

    private void SkipQuestion(object sender, RoutedEventArgs e) => CompleteQuestion(null);
    private void RevealAnswer(object sender, RoutedEventArgs e)
    {
        AnswerButton.Visibility = Visibility.Collapsed;
        AnswerText.Visibility = Visibility.Visible;
        Motion.Enter(AnswerText);
    }

    private void ToggleTimer(object sender, RoutedEventArgs e)
    {
        if (timer.IsEnabled) timer.Stop();
        else if (secondsLeft > 0) timer.Start();
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        string time = TimeSpan.FromSeconds(secondsLeft).ToString(@"mm\:ss");
        if (secondsLeft <= 5 && TimerText.Text != time) Motion.Pulse(TimerText);
        TimerText.Text = time;
        TimerText.Foreground = secondsLeft == 0 ? Brushes.DarkRed : (Brush)FindResource("AccentBrush");
        TimerButton.Content = timer.IsEnabled ? "Пауза" : "Старт";
        projector?.UpdateCountdown(session.Question is null ? null : secondsLeft);
    }

    private void OpenProjector(object sender, RoutedEventArgs e)
    {
        var audienceScreen = DisplayPlacement.FindAudienceScreen(this);
        if (projector == null)
        {
            projector = new ProjectorWindow { ShowInTaskbar = true };
            projector.Closed += (_, _) => projector = null;
        }
        if (audienceScreen.HasValue)
        {
            DisplayPlacement.ShowOnScreen(projector, audienceScreen.Value);
        }
        else
        {
            // With one monitor, provide a movable, resizable audience window.
            projector.WindowStyle = WindowStyle.SingleBorderWindow;
            projector.ResizeMode = ResizeMode.CanResize;
            projector.Width = 1000;
            projector.Height = 700;
            projector.Show();
            projector.Activate();
        }
        RefreshProjector();
        // Keep keyboard and mouse interaction in the host window.
        if (audienceScreen.HasValue) Activate();
    }

    private void RefreshProjector() =>
        projector?.Update(session.Game?.Title ?? "", session.Topic?.Title ?? "", session.Question,
            secondsLeft, teams, session.Game, session.PlayedQuestions, questionVideo);
    private static void ShowError(Exception exception) => MessageBox.Show("Не удалось выполнить операцию: " + exception.Message, "Quiz Helper", MessageBoxButton.OK, MessageBoxImage.Error);
}
