using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Svoyak;

public partial class MainWindow : Window
{
    private readonly GameRepository repository;
    private readonly TimerSettings timerSettings = new();
    private readonly List<Team> teams = [];
    private readonly HashSet<int> playedQuestions = [];
    private readonly Stack<ScoreAction> history = new();
    private readonly HashSet<(int QuestionId, Team Team)> wrongAnswers = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Game? draft;
    private Game? currentGame;
    private Question? currentQuestion;
    private Topic? currentTopic;
    private int secondsLeft;
    private ProjectorWindow? projector;
    private record ScoreAction(Team? Team, int Delta, int? QuestionId, string Description, int? WrongQuestionId = null);

    public MainWindow() : this(new GameRepository()) { }

    public MainWindow(GameRepository repository)
    {
        this.repository = repository;
        InitializeComponent();
        timer.Tick += (_, _) =>
        {
            secondsLeft = Math.Max(0, secondsLeft - 1);
            if (secondsLeft == 0) timer.Stop();
            UpdateTimer();
        };
        Closed += (_, _) => { timer.Stop(); CloseQuestionMedia(); projector?.Close(); };
        teams.Add(new Team { Name = "Эрудиты" });
        teams.Add(new Team { Name = "Знатоки" });
    }

    private void ShowView(FrameworkElement view, string mode = "")
    {
        foreach (var screen in new FrameworkElement[] { MenuView, LibraryView, EditorView, SetupView, BoardView, QuestionView, ResultsView })
            screen.Visibility = screen == view ? Visibility.Visible : Visibility.Collapsed;
        MenuButton.Visibility = view == MenuView ? Visibility.Collapsed : Visibility.Visible;
        ModeLabel.Text = mode;
        if (view != QuestionView)
        {
            timer.Stop();
            CloseQuestionMedia();
        }
    }

    private static Button ActionButton(object content, Action action)
    {
        var button = new Button { Content = content };
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock Text(string value, double size = 14) => new()
    {
        Text = value, FontSize = size, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0)
    };

    private static Grid ActionRow(UIElement caption, params Button[] buttons)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(caption);
        for (int index = 0; index < buttons.Length; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var button = buttons[index];
            button.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(button, index + 1);
            row.Children.Add(button);
        }
        return row;
    }

    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        projector?.Close();
        ShowView(MenuView);
    }

    private void OpenLibrary(object sender, RoutedEventArgs e)
    {
        ShowView(LibraryView, "Режим: редактор");
        LibraryRows.Children.Clear();
        try
        {
            foreach (var game in repository.LoadGames())
            {
                var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                var caption = new StackPanel();
                caption.Children.Add(Text(game.Title, 16));
                caption.Children.Add(Text($"{game.Topics.Count} тем · {game.Topics.Sum(topic => topic.Questions.Count)} вопросов", 13));

                var open = ActionButton(caption, () => EditGame(game));
                open.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                open.VerticalAlignment = VerticalAlignment.Stretch;
                open.Padding = new Thickness(10, 6, 80, 6);
                open.Margin = new Thickness(0);
                System.Windows.Automation.AutomationProperties.SetName(open, $"Открыть игру «{game.Title}» в редакторе");
                row.Children.Add(open);

                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                actions.Children.Add(LibraryAction("CopyRowButton", "Копировать игру", () =>
                {
                    var copy = GameRepository.Copy(game, false);
                    copy.Title += " (копия)";
                    repository.Save(copy);
                    OpenLibrary(sender, e);
                }));
                actions.Children.Add(LibraryAction("DeleteRowButton", "Удалить игру", () =>
                {
                    if (!ConfirmDeletion($"Удалить игру «{game.Title}» со всеми темами и вопросами?")) return;
                    repository.Delete(game.Id);
                    OpenLibrary(sender, e);
                }));
                row.Children.Add(actions);
                LibraryRows.Children.Add(row);
            }
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private Button LibraryAction(string style, string label, Action action)
    {
        var button = ActionButton(label, () =>
        {
            try { action(); }
            catch (Exception exception) { ShowError(exception); }
        });
        button.Style = (Style)FindResource(style);
        button.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    private void OpenSetup(object sender, RoutedEventArgs e)
    {
        try { GameList.ItemsSource = repository.LoadGames(); GameList.SelectedIndex = 0; }
        catch (Exception exception) { ShowError(exception); }
        RenderTeamInputs();
        ShowView(SetupView, "Режим: игра");
    }

    private void EditGameFromSetup(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Game game }) EditGame(game);
    }

    private void RenderTeamInputs()
    {
        TeamCount.Text = teams.Count.ToString();
        TeamInputs.Children.Clear();
        foreach (var team in teams)
        {
            var panel = new StackPanel { Margin = teams.IndexOf(team) % 2 == 0 ? new Thickness(0, 0, 6, 0) : new Thickness(6, 0, 0, 0) };
            panel.Children.Add(Text($"Команда {teams.IndexOf(team) + 1}", 13));
            var input = new TextBox { Text = team.Name };
            input.TextChanged += (_, _) => team.Name = input.Text;
            panel.Children.Add(input);
            TeamInputs.Children.Add(panel);
        }
    }

    private void AddTeam(object sender, RoutedEventArgs e)
    {
        if (teams.Count == 8) return;
        teams.Add(new Team { Name = $"Команда {teams.Count + 1}" });
        RenderTeamInputs();
    }

    private bool ConfirmDeletion(string message) =>
        MessageBox.Show(this, message, "Подтверждение удаления", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void RemoveTeam(object sender, RoutedEventArgs e)
    {
        if (teams.Count <= 2 || !ConfirmDeletion($"Удалить команду «{teams[^1].Name}»?")) return;
        teams.RemoveAt(teams.Count - 1);
        RenderTeamInputs();
    }

}
