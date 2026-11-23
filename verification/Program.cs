using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Svoyak;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string ScreenshotDirectory = Path.GetFullPath("artifacts");

    [STAThread]
    private static void Main()
    {
        Directory.CreateDirectory(ScreenshotDirectory);
        var repository = CreateTestRepository();
        var game = SaveTestGame(repository);
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow(repository);
        window.Show();

        Render(window, "menu");
        Invoke(window, "EditGame", game);
        Render(window, "editor");
        Invoke(window, "OpenSetup", window, new RoutedEventArgs());
        var games = Find<ListBox>(window, "GameList");
        game = games.Items.OfType<Game>().Single(item => item.Id == game.Id);
        games.SelectedItem = game;
        Render(window, "setup");
        Invoke(window, "StartGame", window, new RoutedEventArgs());
        Render(window, "board");

        CheckRound(window, game);
        CheckEditor(window, game);
        CheckCompactViews(window, game);

        window.Close();
        app.Shutdown();
        Console.WriteLine("PASS: repository, editor, compact views, scoring, undo, and projector answer reveal");
    }

    private static GameRepository CreateTestRepository()
    {
        var database = Path.Combine(AppContext.BaseDirectory, "verification.db");
        File.Copy(Path.GetFullPath("../SQLite_Svoyak.db"), database, overwrite: true);
        return new GameRepository(database);
    }

    private static Game SaveTestGame(GameRepository repository)
    {
        var question = new Question
        {
            Text = "В каком году началась Первая мировая война?",
            Answer = "1914",
            Points = 100
        };
        var game = new Game
        {
            Title = "Общая эрудиция",
            Topics = [new Topic { Title = "История", Questions = [question] }]
        };
        repository.Save(game);
        repository.Save(game);
        var stored = repository.LoadGames().Single(item => item.Id == game.Id);
        Require(stored.Topics.Count == 1 && stored.Topics[0].Questions.Count == 1, "Save failed");
        return stored;
    }

    private static void CheckRound(MainWindow window, Game game)
    {
        var topic = game.Topics[0];
        var question = topic.Questions[0];
        question.MediaType = 1;
        question.MediaPath = Path.GetFullPath("../Svoyak/Assets/logo.png");

        var audience = (ProjectorWindow)typeof(MainWindow).GetField("projector", PrivateInstance)!.GetValue(window)!;
        Require(audience.IsVisible, "Audience window did not open");
        OpenQuestion(window, topic, question);
        Render(window, "question");
        Render(audience, "audience-image");
        var image = Find<Image>(audience, "QuestionPicture");
        Require(image.Source != null && image.ActualHeight > 0, "Audience image missing");

        var answerRows = Find<Panel>(window, "AnswerTeams");
        var firstTeam = (Panel)answerRows.Children[0];
        var scoreRows = Find<Panel>(window, "ScoreRows");
        var wrong = firstTeam.Children.OfType<Button>().Last();
        Click(wrong);
        Require(!wrong.IsEnabled, "Wrong-answer button still active");
        Click(wrong);
        Require(GetScore(scoreRows) == "-100", "Wrong-answer penalty must apply once");
        Click(Find<Button>(window, "UndoButton"));
        Require(GetScore(scoreRows) == "0", "Penalty undo failed");

        OpenQuestion(window, topic, question);
        firstTeam = (Panel)answerRows.Children[0];
        Require(firstTeam.Children.OfType<Button>().Last().IsEnabled, "Penalty undo did not restore the button");
        Click(firstTeam.Children.OfType<Button>().First());
        Require(GetScore(scoreRows) == "100", "Correct answer did not award points");
        Require(Find<FrameworkElement>(audience, "AnswerView").Visibility == Visibility.Visible, "Answer did not appear");
        Require(Find<TextBlock>(audience, "AnswerText").Text == question.Answer, "Wrong answer displayed");
        PumpDispatcher(TimeSpan.FromMilliseconds(4250));
        Require(Find<FrameworkElement>(audience, "AnswerView").Visibility == Visibility.Collapsed, "Answer did not expire");

        Render(audience, "audience-board");
        var board = Find<Grid>(audience, "AudienceBoard");
        Require(board.Children.Count == 2 && !board.IsHitTestVisible, "Audience board must be visible and non-interactive");
        var cell = (Border)board.Children[1];
        var label = (TextBlock)cell.Child;
        Require(label.Text == "100" && label.TextDecorations?.Count is not > 0 && cell.Opacity < 1, "Played cell missing");
        Require(Find<FrameworkElement>(audience, "QuestionView").Visibility == Visibility.Collapsed, "Question must hide on board");
        Require(image.Source is null, "Stale audience image");

        Click(Find<Button>(window, "UndoButton"));
        Require(GetScore(scoreRows) == "0", "Correct-answer undo failed");
        OpenQuestion(window, topic, question);
        Invoke(window, "SkipQuestion", window, new RoutedEventArgs());
        Require(GetScore(scoreRows) == "0", "Skipping changed the score");
        Require(Find<FrameworkElement>(audience, "AnswerView").Visibility == Visibility.Visible, "Skipping did not reveal the answer");
        audience.Close();
    }

    private static void CheckEditor(MainWindow window, Game game)
    {
        Invoke(window, "EditGame", game);
        var draft = (Game)typeof(MainWindow).GetField("draft", PrivateInstance)!.GetValue(window)!;
        Invoke(window, "RemoveTopicFromDraft", draft.Topics[0]);
        Require(draft.Topics.Count == 0, "Topic deletion failed");
        Invoke(window, "AddTopic", window, new RoutedEventArgs());
        Require(draft.Topics.Count == 1, "Adding after deletion failed");
        Invoke(window, "OpenLibrary", window, new RoutedEventArgs());
        Render(window, "library");
        Invoke(window, "FinishGame", window, new RoutedEventArgs());
        Render(window, "results");
    }

    private static void CheckCompactViews(MainWindow window, Game game)
    {
        window.Width = 1050;
        window.Height = 680;
        Invoke(window, "OpenSetup", window, new RoutedEventArgs());
        for (int index = 0; index < 6; index++)
            Invoke(window, "AddTeam", window, new RoutedEventArgs());
        Render(window, "setup-compact-eight");
        Invoke(window, "EditGame", game);
        Render(window, "editor-compact");
        OpenQuestion(window, game.Topics[0], game.Topics[0].Questions[0]);
        Render(window, "question-compact");
    }

    private static string GetScore(Panel rows) => ((Panel)rows.Children[0]).Children.OfType<TextBox>().Single().Text;
    private static void OpenQuestion(MainWindow window, Topic topic, Question question) => Invoke(window, "OpenQuestion", topic, question);
    private static T Find<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Invoke(MainWindow window, string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, PrivateInstance)!.Invoke(window, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Render(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(ScreenshotDirectory, name + ".png"));
        encoder.Save(file);
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
