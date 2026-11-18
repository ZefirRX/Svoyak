using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Svoyak;

public partial class MainWindow
{
    private QuestionAudioPlayer? audioPlayer;
    private MediaPlayer? videoPlayer;
    private DrawingImage? questionVideo;

    private void CloseQuestionMedia()
    {
        audioPlayer?.Dispose();
        audioPlayer = null;
        videoPlayer?.Close();
        videoPlayer = null;
        questionVideo = null;
        MediaPanel.Children.Clear();
    }

    private void RenderMedia(Question question)
    {
        CloseQuestionMedia();
        var mediaPath = question.MediaPath;
        bool isImage = question.MediaType == 1;
        MediaFrame.BorderThickness = new Thickness(isImage ? 0 : 1);
        MediaFrame.Padding = new Thickness(isImage ? 0 : 8);
        MediaFrame.Visibility = !string.IsNullOrEmpty(mediaPath) ? Visibility.Visible : Visibility.Collapsed;
        MediaFrame.VerticalAlignment = question.MediaType == 3 || question.MediaType == 1
            ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        if (string.IsNullOrEmpty(mediaPath)) return;

        try
        {
            if (!System.IO.File.Exists(mediaPath))
                throw new System.IO.FileNotFoundException("Файл не найден. Выберите его заново в редакторе.");
            if (question.MediaType == 1)
            {
                MediaPanel.Children.Add(new Image { Source = QuestionImage.Load(mediaPath), Stretch = Stretch.Uniform });
                return;
            }

            var panel = new StackPanel();
            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            var status = Text(System.IO.Path.GetFileName(mediaPath));
            status.Margin = new Thickness(0, 0, 0, 5);
            panel.Children.Add(status);
            panel.Children.Add(controls);
            DockPanel.SetDock(panel, Dock.Top);

            void ShowPlaybackError(string error)
            {
                status.Text = "Не удалось воспроизвести файл: " + error;
                controls.IsEnabled = false;
            }

            async void RunAudioCommand(Func<Task> command)
            {
                try { await command(); }
                catch (Exception error)
                {
                    if (panel.IsLoaded) ShowPlaybackError(error.Message);
                }
            }

            Button AddPlaybackControls(Action toggle, Action restart)
            {
                var toggleButton = ActionButton("▶ Воспроизвести", toggle);
                toggleButton.Margin = new Thickness(0, 0, 6, 5);
                controls.Children.Add(toggleButton);
                controls.Children.Add(ActionButton("↻ С начала", restart));
                return toggleButton;
            }

            static void UpdatePlaybackButton(Button button, bool playing) =>
                button.Content = playing ? "Ⅱ Пауза" : "▶ Воспроизвести";

            MediaPanel.Children.Add(panel);
            if (question.MediaType == 2)
            {
                var player = new QuestionAudioPlayer();
                audioPlayer = player;
                controls.IsEnabled = false;
                var toggleButton = AddPlaybackControls(
                    () => RunAudioCommand(player.ToggleAsync),
                    () => RunAudioCommand(player.RestartAsync));
                status.Text += " · Загрузка…";
                panel.Children.Add(player.View);
                player.View.Loaded += OpenAudio;

                async void OpenAudio(object sender, RoutedEventArgs e)
                {
                    player.View.Loaded -= OpenAudio;
                    await player.OpenAsync(mediaPath, () =>
                    {
                        status.Text = System.IO.Path.GetFileName(mediaPath);
                        controls.IsEnabled = true;
                    }, playing => UpdatePlaybackButton(toggleButton, playing), ShowPlaybackError);
                }
            }
            else
            {
                var media = new MediaPlayer { ScrubbingEnabled = true };
                var drawing = new VideoDrawing { Player = media, Rect = new Rect(0, 0, 16, 9) };
                videoPlayer = media;
                questionVideo = new DrawingImage(drawing);
                media.MediaOpened += (_, _) =>
                {
                    if (media.NaturalVideoWidth > 0 && media.NaturalVideoHeight > 0)
                        drawing.Rect = new Rect(0, 0, media.NaturalVideoWidth, media.NaturalVideoHeight);
                };
                media.MediaFailed += (_, e) => ShowPlaybackError(e.ErrorException.Message);
                bool playing = false;
                var toggleButton = ActionButton("▶ Воспроизвести", () =>
                {
                    if (playing) media.Pause();
                    else media.Play();
                    playing = !playing;
                });
                toggleButton.Click += (_, _) => UpdatePlaybackButton(toggleButton, playing);
                toggleButton.Margin = new Thickness(0, 0, 6, 5);
                controls.Children.Add(toggleButton);
                controls.Children.Add(ActionButton("↻ С начала", () =>
                {
                    media.Position = TimeSpan.Zero;
                    media.Play();
                    playing = true;
                    UpdatePlaybackButton(toggleButton, playing);
                }));
                media.MediaEnded += (_, _) =>
                {
                    media.Stop();
                    playing = false;
                    UpdatePlaybackButton(toggleButton, playing);
                };
                MediaPanel.Children.Add(new Image { Source = questionVideo, Stretch = Stretch.Uniform });
                media.Open(new Uri(System.IO.Path.GetFullPath(mediaPath)));
            }
        }
        catch (Exception exception)
        {
            CloseQuestionMedia();
            MediaFrame.VerticalAlignment = VerticalAlignment.Top;
            MediaPanel.Children.Add(Text("Не удалось открыть медиа: " + exception.Message));
        }
    }
}
