using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Svoyak;

public partial class MainWindow : Window
{
    private readonly GameRepository repository = new();
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

    public MainWindow()
    {
        InitializeComponent();
        timer.Tick += (_, _) =>
        {
            secondsLeft = Math.Max(0, secondsLeft - 1);
            if (secondsLeft == 0) timer.Stop();
            UpdateTimer();
        };
        Closed += (_, _) => { timer.Stop(); projector?.Close(); };
        for (int index = 0; index < 2; index++)
            teams.Add(new Team { Name = new[] { "Эрудиты", "Знатоки", "Совы", "Лисы" }[index] });
    }

    private void ShowView(FrameworkElement view, string mode = "")
    {
        foreach (var screen in new FrameworkElement[] { MenuView, LibraryView, EditorView, SetupView, BoardView, QuestionView, ResultsView })
            screen.Visibility = screen == view ? Visibility.Visible : Visibility.Collapsed;
        ModeLabel.Text = mode;
        if (view != QuestionView) timer.Stop();
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text };
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock Text(string value, double size = 15) => new()
    {
        Text = value, FontSize = size, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0)
    };

    private static Grid ActionRow(UIElement caption, params Button[] buttons)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(caption);
        for (int index = 0; index < buttons.Length; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var button = buttons[index];
            button.Margin = new Thickness(8, 0, 0, 0);
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
                var row = new DockPanel { Margin = new Thickness(0, 8, 0, 8), LastChildFill = true };
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                actions.Children.Add(ActionButton("Редактировать", () => EditGame(game)));
                actions.Children.Add(ActionButton("Копия", () =>
                {
                    var copy = GameRepository.Copy(game, false);
                    copy.Title += " (копия)";
                    repository.Save(copy);
                    OpenLibrary(sender, e);
                }));
                actions.Children.Add(ActionButton("Удалить", () =>
                {
                    if (MessageBox.Show($"Удалить игру «{game.Title}»?", "Удаление", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                    repository.Delete(game.Id);
                    OpenLibrary(sender, e);
                }));
                for (int index = 0; index < actions.Children.Count; index++)
                    ((FrameworkElement)actions.Children[index]).Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(actions, Dock.Right);
                row.Children.Add(actions);
                var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                caption.Children.Add(Text(game.Title, 18));
                caption.Children.Add(Text($"{game.Topics.Count} тем · {game.Topics.Sum(topic => topic.Questions.Count)} вопросов", 13));
                row.Children.Add(caption);
                LibraryRows.Children.Add(row);
            }
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void OpenSetup(object sender, RoutedEventArgs e)
    {
        try { GameList.ItemsSource = repository.LoadGames(); GameList.SelectedIndex = 0; }
        catch (Exception exception) { ShowError(exception); }
        RenderTeamInputs();
        ShowView(SetupView, "Режим: игра");
    }

    private void EditSelectedGame(object sender, RoutedEventArgs e)
    {
        if (GameList.SelectedItem is Game game) EditGame(game);
    }

    private void RenderTeamInputs()
    {
        TeamCount.Text = teams.Count.ToString();
        TeamInputs.Children.Clear();
        foreach (var team in teams)
        {
            var panel = new StackPanel { Margin = teams.IndexOf(team) % 2 == 0 ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0) };
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

    private void RemoveTeam(object sender, RoutedEventArgs e)
    {
        if (teams.Count <= 2) return;
        teams.RemoveAt(teams.Count - 1);
        RenderTeamInputs();
    }

}
