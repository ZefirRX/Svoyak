using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Svoyak;

public partial class MainWindow
{
    private void CreateGame(object sender, RoutedEventArgs e) => EditGame(new Game
    {
        Topics = [new Topic { Title = "Тема 1", Questions = [new Question()] }]
    });

    private void EditGame(Game game)
    {
        draft = GameRepository.Copy(game);
        GameTitle.DataContext = draft;
        RefreshTopics();
        ShowView(EditorView, "Режим: редактор");
    }

    private void RefreshTopics()
    {
        TopicList.ItemsSource = null;
        TopicList.ItemsSource = draft?.Topics;
        TopicList.SelectedIndex = 0;
    }

    private void SelectTopic(object sender, SelectionChangedEventArgs e)
    {
        var topic = TopicList.SelectedItem as Topic;
        TopicTitle.DataContext = topic;
        QuestionList.ItemsSource = topic?.Questions;
        QuestionList.SelectedIndex = 0;
    }

    private string TimerKey(int gameId, int topicIndex, int questionIndex) => $"{gameId}:{topicIndex}:{questionIndex}";
    private string DraftTimerKey() => TimerKey(draft?.Id ?? 0, TopicList.SelectedIndex, QuestionList.SelectedIndex);

    private void SelectQuestion(object sender, SelectionChangedEventArgs e)
    {
        QuestionForm.DataContext = QuestionList.SelectedItem;
        SecondsInput.Text = timerSettings.Get(DraftTimerKey()).ToString();
    }

    private void AddTopic(object sender, RoutedEventArgs e)
    {
        if (draft == null) return;
        draft.Topics.Add(new Topic { Questions = [new Question()] });
        RefreshTopics();
        TopicList.SelectedIndex = draft.Topics.Count - 1;
    }

    private void DeleteTopic(object sender, RoutedEventArgs e)
    {
        if (draft == null || TopicList.SelectedItem is not Topic topic) return;
        int removedIndex = TopicList.SelectedIndex;
        timerSettings.RemoveTopic(draft.Id, removedIndex);
        draft.Topics.Remove(topic);
        RefreshTopics();
        TopicList.SelectedIndex = Math.Min(removedIndex, draft.Topics.Count - 1);
    }

    private void AddQuestion(object sender, RoutedEventArgs e)
    {
        if (TopicList.SelectedItem is not Topic topic) return;
        topic.Questions.Add(new Question { Points = (topic.Questions.Count + 1) * 100 });
        QuestionList.ItemsSource = null;
        QuestionList.ItemsSource = topic.Questions;
        QuestionList.SelectedIndex = topic.Questions.Count - 1;
    }

    private void DeleteQuestion(object sender, RoutedEventArgs e)
    {
        if (TopicList.SelectedItem is not Topic topic || QuestionList.SelectedItem is not Question question) return;
        topic.Questions.Remove(question);
        QuestionList.ItemsSource = null;
        QuestionList.ItemsSource = topic.Questions;
        QuestionList.SelectedIndex = 0;
    }

    private void SaveTimerSetting(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(SecondsInput.Text, out var seconds) && seconds >= 0 && seconds <= 86400)
            timerSettings.Set(DraftTimerKey(), seconds);
        else SecondsInput.Text = "30";
    }

    private void ChooseMedia(object sender, RoutedEventArgs e)
    {
        if (QuestionList.SelectedItem is not Question question) return;
        var dialog = new OpenFileDialog { Filter = "Медиа|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp3;*.wav;*.mp4;*.wmv" };
        if (dialog.ShowDialog() != true) return;
        question.MediaPath = dialog.FileName;
        var extension = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
        question.MediaType = extension is ".mp3" or ".wav" ? 2 : extension is ".mp4" or ".wmv" ? 3 : 1;
        QuestionForm.DataContext = null;
        QuestionForm.DataContext = question;
    }

    private bool SaveDraft()
    {
        if (draft == null) return false;
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Topics.Count == 0 ||
            draft.Topics.Any(topic => string.IsNullOrWhiteSpace(topic.Title) || topic.Questions.Count == 0 ||
                topic.Questions.Any(question => string.IsNullOrWhiteSpace(question.Text) || string.IsNullOrWhiteSpace(question.Answer) || question.Points < 0)) || Validation.GetHasError(PointsInput))
        {
            MessageBox.Show("Заполните название игры, темы, тексты вопросов, ответы и неотрицательные баллы.");
            return false;
        }
        try
        {
            SaveTimerSetting(this, new RoutedEventArgs());
            int previousId = draft.Id;
            repository.Save(draft);
            if (previousId == 0)
            {
                for (int topicIndex = 0; topicIndex < draft.Topics.Count; topicIndex++)
                for (int questionIndex = 0; questionIndex < draft.Topics[topicIndex].Questions.Count; questionIndex++)
                    timerSettings.Set(TimerKey(draft.Id, topicIndex, questionIndex),
                        timerSettings.Get(TimerKey(previousId, topicIndex, questionIndex)));
            }
            timerSettings.Save();
            return true;
        }
        catch (Exception exception) { ShowError(exception); return false; }
    }

    private void SaveGame(object sender, RoutedEventArgs e)
    {
        if (SaveDraft()) MessageBox.Show("Игра сохранена.");
    }

    private void SaveAndExit(object sender, RoutedEventArgs e)
    {
        if (SaveDraft()) OpenLibrary(sender, e);
    }

}
