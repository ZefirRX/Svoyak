using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

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
        QuestionList.ItemsSource = topic?.Questions;
        QuestionList.SelectedIndex = 0;
    }

    private void FocusTopicTitle(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: Topic topic })
            TopicList.SelectedItem = topic;
    }

    private string DraftTimerKey() => TimerSettings.Key(draft?.Id ?? 0, TopicList.SelectedIndex, QuestionList.SelectedIndex);

    private void SelectQuestion(object sender, SelectionChangedEventArgs e)
    {
        var question = QuestionList.SelectedItem as Question;
        QuestionForm.DataContext = question;
        ShowEditorPreview(question);
        if (question != null && EditorView.IsVisible) Motion.Enter(QuestionForm, 5, 180);
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
        if (sender is Button { DataContext: Topic topic } &&
            ConfirmDeletion($"Удалить тему «{topic.Title}» со всеми вопросами?"))
            RemoveTopicFromDraft(topic);
    }

    private void RemoveTopicFromDraft(Topic topic)
    {
        if (draft == null) return;
        int removedIndex = draft.Topics.IndexOf(topic);
        if (removedIndex < 0) return;
        var selected = TopicList.SelectedItem as Topic;
        timerSettings.RemoveTopic(draft.Id, removedIndex);
        draft.Topics.Remove(topic);
        RefreshTopics();
        TopicList.SelectedIndex = selected == topic || selected == null
            ? Math.Min(removedIndex, draft.Topics.Count - 1) : draft.Topics.IndexOf(selected);
    }

    private void AddQuestion(object sender, RoutedEventArgs e)
    {
        if (TopicList.SelectedItem is not Topic topic) return;
        topic.Questions.Add(new Question { Points = (topic.Questions.Count + 1) * 100 });
        RefreshQuestions(topic, topic.Questions.Count - 1);
    }

    private void DeleteQuestion(object sender, RoutedEventArgs e)
    {
        if (TopicList.SelectedItem is not Topic topic || sender is not Button { DataContext: Question question }) return;
        int removedIndex = topic.Questions.IndexOf(question);
        if (removedIndex < 0) return;

        string prompt = string.IsNullOrWhiteSpace(question.Text) ? "Удалить этот вопрос?" : $"Удалить вопрос?\n\n{question.Text}";
        if (!ConfirmDeletion(prompt)) return;

        var selected = QuestionList.SelectedItem as Question;
        topic.Questions.RemoveAt(removedIndex);
        RefreshQuestions(topic, selected == question || selected == null
            ? Math.Min(removedIndex, topic.Questions.Count - 1) : topic.Questions.IndexOf(selected));
    }

    private void RefreshQuestions(Topic topic, int selection)
    {
        QuestionList.ItemsSource = null;
        QuestionList.ItemsSource = topic.Questions;
        QuestionList.SelectedIndex = selection;
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
        var dialog = new OpenFileDialog { Filter = "Медиа|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp3;*.wav;*.m4a;*.mp4;*.wmv" };
        if (dialog.ShowDialog() != true) return;
        question.MediaPath = dialog.FileName;
        var extension = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
        question.MediaType = extension switch
        {
            ".mp3" or ".wav" or ".m4a" => QuestionMediaType.Audio,
            ".mp4" or ".wmv" => QuestionMediaType.Video,
            _ => QuestionMediaType.Image
        };
        QuestionForm.DataContext = null;
        QuestionForm.DataContext = question;
        ShowEditorPreview(question);
        if (EditorImagePreview.IsVisible) Motion.Enter(EditorImagePreview);
        if (EditorVideoPreview.IsVisible) Motion.Enter(EditorVideoPreview);
    }

    private bool editorVideoPlaying;

    private void ClearEditorPreview()
    {
        if (EditorVideo.Source != null)
        {
            EditorVideo.Close();
            EditorVideo.Source = null;
        }
        EditorImagePreview.Source = null;
        EditorImagePreview.Visibility = Visibility.Collapsed;
        EditorVideoPreview.Visibility = Visibility.Collapsed;
        EditorPreviewError.Visibility = Visibility.Collapsed;
        EditorVideoToggle.IsEnabled = false;
        SetEditorVideoPlaying(false);
    }

    private void ShowEditorPreview(Question? question)
    {
        ClearEditorPreview();
        if (question?.MediaPath is not { Length: > 0 } path ||
            question.MediaType is not (QuestionMediaType.Image or QuestionMediaType.Video))
            return;

        try
        {
            if (!System.IO.File.Exists(path))
            {
                ShowEditorPreviewError("Медиафайл не найден. Выберите его заново.");
                return;
            }

            if (question.MediaType == QuestionMediaType.Image)
            {
                EditorImagePreview.Source = QuestionImage.Load(path);
                EditorImagePreview.Visibility = Visibility.Visible;
            }
            else
            {
                EditorVideo.Source = new Uri(System.IO.Path.GetFullPath(path));
                EditorVideoPreview.Visibility = Visibility.Visible;
                EditorVideo.Pause();
            }
        }
        catch (Exception exception)
        {
            ShowEditorPreviewError("Не удалось открыть медиа: " + exception.Message);
        }
    }

    private void EditorVideoOpened(object sender, RoutedEventArgs e)
    {
        if (EditorVideo.Source == null) return;
        EditorVideo.Position = TimeSpan.Zero;
        EditorVideoToggle.IsEnabled = true;
    }

    private void ToggleEditorVideo(object sender, RoutedEventArgs e)
    {
        if (editorVideoPlaying) EditorVideo.Pause();
        else EditorVideo.Play();
        SetEditorVideoPlaying(!editorVideoPlaying);
    }

    private void EditorVideoEnded(object sender, RoutedEventArgs e)
    {
        if (EditorVideo.Source == null) return;
        EditorVideo.Pause();
        EditorVideo.Position = TimeSpan.Zero;
        SetEditorVideoPlaying(false);
    }

    private void SetEditorVideoPlaying(bool playing)
    {
        editorVideoPlaying = playing;
        EditorVideoToggle.Content = playing ? "Ⅱ Пауза" : "▶ Воспроизвести";
    }

    private void EditorVideoFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (EditorVideo.Source != null)
            ShowEditorPreviewError("Не удалось открыть видео: " + e.ErrorException.Message);
    }

    private void ShowEditorPreviewError(string message)
    {
        ClearEditorPreview();
        EditorPreviewError.Text = message;
        EditorPreviewError.Visibility = Visibility.Visible;
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
                    timerSettings.Set(TimerSettings.Key(draft.Id, topicIndex, questionIndex),
                        timerSettings.Get(TimerSettings.Key(previousId, topicIndex, questionIndex)));
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
