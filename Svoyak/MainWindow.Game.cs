using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        currentGame = game;
        playedQuestions.Clear();
        history.Clear();
        wrongAnswers.Clear();
        foreach (var team in teams) { team.Score = 0; if (string.IsNullOrWhiteSpace(team.Name)) team.Name = "Команда"; }
        ReturnToBoard(sender, e);
        OpenProjector(sender, e);
    }

    private void ReturnToBoard(object sender, RoutedEventArgs e)
    {
        if (currentGame == null) return;
        currentQuestion = null;
        ShowView(BoardView, "Режим: игра");
        BoardTitle.Text = $"{currentGame.Title}   Сыграно {playedQuestions.Count} из {currentGame.Topics.Sum(topic => topic.Questions.Count)}";
        BoardCells.Children.Clear();
        BoardCells.ColumnDefinitions.Clear();
        BoardCells.RowDefinitions.Clear();
        int rowCount = currentGame.Topics.Max(topic => topic.Questions.Count);
        for (int column = 0; column < currentGame.Topics.Count; column++) BoardCells.ColumnDefinitions.Add(new ColumnDefinition());
        BoardCells.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        for (int row = 0; row < rowCount; row++) BoardCells.RowDefinitions.Add(new RowDefinition { Height = new GridLength(88) });
        for (int column = 0; column < currentGame.Topics.Count; column++)
        {
            var topic = currentGame.Topics[column];
            var header = new Border { Background = (Brush)FindResource("AccentBrush"), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 8, 8), Child = new TextBlock { Text = topic.Title, Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            PutCell(header, column, 0);
            for (int row = 0; row < topic.Questions.Count; row++)
            {
                var question = topic.Questions[row];
                var button = ActionButton(question.Points.ToString(), () => OpenQuestion(topic, question));
                button.Margin = new Thickness(0, 0, 8, 8);
                button.VerticalAlignment = VerticalAlignment.Stretch;
                button.FontSize = 38;
                button.FontWeight = FontWeights.Bold;
                button.Foreground = (Brush)FindResource("AccentBrush");
                button.IsEnabled = !playedQuestions.Contains(question.Id);
                if (!button.IsEnabled) button.Content = new TextBlock { Text = question.Points.ToString(), TextDecorations = TextDecorations.Strikethrough };
                PutCell(button, column, row + 1);
            }
        }
        RenderScores();
        RefreshProjector();
    }

    private void PutCell(UIElement element, int column, int row)
    {
        Grid.SetColumn(element, column);
        Grid.SetRow(element, row);
        BoardCells.Children.Add(element);
    }

    private void RenderScores()
    {
        ScoreRows.Children.Clear();
        foreach (var team in teams)
        {
            var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            caption.Children.Add(Text(team.Name));
            var score = Text(team.Score.ToString(), 28);
            score.Foreground = (Brush)FindResource("AccentBrush");
            score.FontWeight = FontWeights.Bold;
            caption.Children.Add(score);
            ScoreRows.Children.Add(ActionRow(caption,
                ActionButton("−100", () => AdjustScore(team, -100)),
                ActionButton("+100", () => AdjustScore(team, 100))));
        }
        LastAction.Text = history.TryPeek(out var action) ? "Последнее: " + action.Description : "Действий пока нет";
        UndoButton.IsEnabled = history.Count > 0;
    }

    private void AdjustScore(Team team, int delta)
    {
        team.Score += delta;
        history.Push(new ScoreAction(team, delta, null, $"{team.Name} {delta:+0;-0} (вручную)"));
        RenderScores();
        RefreshProjector();
    }

    private void UndoScore(object sender, RoutedEventArgs e)
    {
        if (!history.TryPop(out var action)) return;
        if (action.Team != null) action.Team.Score -= action.Delta;
        if (action.QuestionId.HasValue) playedQuestions.Remove(action.QuestionId.Value);
        if (action.WrongQuestionId.HasValue && action.Team != null)
            wrongAnswers.Remove((action.WrongQuestionId.Value, action.Team));
        ReturnToBoard(sender, e);
    }

    private void OpenQuestion(Topic topic, Question question)
    {
        currentTopic = topic;
        currentQuestion = question;
        secondsLeft = timerSettings.Get(TimerKey(currentGame!.Id, currentGame.Topics.IndexOf(topic), topic.Questions.IndexOf(question)));
        QuestionTitle.Text = $"{topic.Title} · {question.Points}";
        QuestionText.Text = question.Text;
        AnswerText.Text = question.Answer;
        AnswerText.Visibility = Visibility.Collapsed;
        AnswerButton.Visibility = Visibility.Visible;
        AnswerTeams.Children.Clear();
        foreach (var team in teams)
        {

            var correct = ActionButton($"Верно +{question.Points}", () => CompleteQuestion(team));
            correct.Background = new SolidColorBrush(Color.FromRgb(30, 127, 85));
            correct.Foreground = Brushes.White;
            bool alreadyWrong = wrongAnswers.Contains((question.Id, team));
            correct.IsEnabled = !alreadyWrong;
            var wrong = new Button
            {
                Content = alreadyWrong ? "Списано ✓" : $"Неверно −{question.Points}",
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
        RenderMedia(question);
        ShowView(QuestionView, "Режим: игра");
        UpdateTimer();
    }

    private void RegisterWrongAnswer(Team team, Question question)
    {
        // A team is penalized once per question, including when the question is reopened.
        if (!wrongAnswers.Add((question.Id, team))) return;
        team.Score -= question.Points;
        history.Push(new ScoreAction(team, -question.Points, null,
            $"{team.Name} −{question.Points} (неверный ответ)", question.Id));
        RenderScores();
        RefreshProjector();
    }

    private void RenderMedia(Question question)
    {
        MediaPanel.Children.Clear();
        if (string.IsNullOrEmpty(question.MediaPath))
        {
            MediaPanel.Children.Add(Text("Медиафайл вопроса (картинка, аудио или видео)"));
            return;
        }
        try
        {
            if (question.MediaType == 1)
                MediaPanel.Children.Add(new Image { Source = QuestionImage.Load(question.MediaPath), Stretch = Stretch.Uniform });
            else
            {
                var media = new MediaElement { Source = new Uri(question.MediaPath), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Stop, MaxHeight = 280 };

                var controls = new StackPanel { Orientation = Orientation.Horizontal };
                controls.Children.Add(ActionButton("▶ Воспроизвести", media.Play));
                controls.Children.Add(ActionButton("Пауза", media.Pause));
                DockPanel.SetDock(controls, Dock.Bottom);
                MediaPanel.Children.Add(controls);
                MediaPanel.Children.Add(media);
            }
        }
        catch (Exception exception) { MediaPanel.Children.Add(Text("Не удалось открыть медиа: " + exception.Message)); }
    }

    private void CompleteQuestion(Team? team)
    {
        if (currentQuestion == null) return;
        int delta = team == null ? 0 : currentQuestion.Points;
        if (team != null) team.Score += delta;
        playedQuestions.Add(currentQuestion.Id);
        history.Push(new ScoreAction(team, delta, currentQuestion.Id, team == null ? "Вопрос без ответа" : $"{team.Name} +{delta} ({currentTopic?.Title})"));
        ReturnToBoard(this, new RoutedEventArgs());
    }

    private void SkipQuestion(object sender, RoutedEventArgs e) => CompleteQuestion(null);
    private void RevealAnswer(object sender, RoutedEventArgs e)
    {
        AnswerButton.Visibility = Visibility.Collapsed;
        AnswerText.Visibility = Visibility.Visible;
    }

    private void ToggleTimer(object sender, RoutedEventArgs e)
    {
        if (timer.IsEnabled) timer.Stop();
        else if (secondsLeft > 0) timer.Start();
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        TimerText.Text = TimeSpan.FromSeconds(secondsLeft).ToString(@"mm\:ss");
        TimerText.Foreground = secondsLeft == 0 ? Brushes.DarkRed : (Brush)FindResource("AccentBrush");
        TimerButton.Content = timer.IsEnabled ? "Пауза" : "Старт";
        RefreshProjector();
    }

    private void FinishGame(object sender, RoutedEventArgs e)
    {
        ShowView(ResultsView, "Режим: игра");
        ResultsTitle.Text = $"ИТОГИ ИГРЫ «{currentGame?.Title.ToUpperInvariant()}»";
        ResultRows.Children.Clear();
        int place = 1;
        foreach (var team in teams.OrderByDescending(team => team.Score))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rank = Text((place++).ToString(), 24);
            var name = Text(team.Name, 24);
            var score = Text(team.Score.ToString(), 28);
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
                Margin = new Thickness(0, 0, 0, 8),
                Child = row
            });
        }
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

    private void RefreshProjector() => projector?.Update(currentGame?.Title ?? "", currentTopic?.Title ?? "", currentQuestion, secondsLeft, teams, currentGame, playedQuestions);
    private static void ShowError(Exception exception) => MessageBox.Show("Не удалось выполнить операцию: " + exception.Message, "Quiz Helper", MessageBoxButton.OK, MessageBoxImage.Error);
}
