using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Svoyak;

public partial class MainWindow
{
    private MediaPlayer? questionPlayer;
    private DrawingImage? questionVideo;
    private AudioPlaybackSource? audioSource;

    private void CloseQuestionMedia()
    {
        var player = questionPlayer;
        questionPlayer = null;
        player?.Close();
        questionVideo = null;
        MediaPanel.Children.Clear();
        audioSource?.Dispose();
        audioSource = null;
    }

    private void RenderMedia(Question question)
    {
        CloseQuestionMedia();
        var mediaPath = question.MediaPath;
        bool isImage = question.MediaType == QuestionMediaType.Image;
        bool isAudio = question.MediaType == QuestionMediaType.Audio;
        MediaFrame.BorderThickness = new Thickness(isImage ? 0 : 1);
        MediaFrame.Padding = new Thickness(isImage ? 0 : 8);
        MediaFrame.Visibility = !string.IsNullOrEmpty(mediaPath) ? Visibility.Visible : Visibility.Collapsed;
        MediaFrame.VerticalAlignment = question.MediaType == QuestionMediaType.Video || isImage
            ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        if (string.IsNullOrEmpty(mediaPath)) return;

        try
        {
            if (!File.Exists(mediaPath))
                throw new FileNotFoundException("Файл не найден. Выберите его заново в редакторе.");
            if (isImage)
            {
                MediaPanel.Children.Add(new Image { Source = QuestionImage.Load(mediaPath), Stretch = Stretch.Uniform });
                return;
            }

            RenderPlayback(mediaPath, isAudio);
        }
        catch (Exception exception)
        {
            CloseQuestionMedia();
            MediaFrame.VerticalAlignment = VerticalAlignment.Top;
            MediaPanel.Children.Add(Text("Не удалось открыть медиа: " + exception.Message));
        }
    }

    private void RenderPlayback(string mediaPath, bool isAudio)
    {
        var panel = new StackPanel();
        var controls = new StackPanel { Orientation = Orientation.Horizontal, IsEnabled = false };
        var fileName = Path.GetFileName(mediaPath);
        var status = Text(fileName + " · Загрузка…");
        status.Margin = new Thickness(0, 0, 0, 5);
        panel.Children.Add(status);
        panel.Children.Add(controls);
        DockPanel.SetDock(panel, Dock.Top);

        MediaPanel.Children.Add(panel);
        var player = new MediaPlayer { ScrubbingEnabled = !isAudio };
        questionPlayer = player;
        VideoDrawing? drawing = null;
        if (!isAudio)
        {
            drawing = new VideoDrawing { Player = player, Rect = new Rect(0, 0, 16, 9) };
            questionVideo = new DrawingImage(drawing);
            MediaPanel.Children.Add(new Image { Source = questionVideo, Stretch = Stretch.Uniform });
        }

        bool playing = false;
        var toggleButton = new Button
        {
            Content = "▶ Воспроизвести",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 5)
        };
        toggleButton.Click += (_, _) =>
        {
            if (playing) player.Pause();
            else player.Play();
            SetPlaying(!playing);
        };
        controls.Children.Add(toggleButton);
        controls.Children.Add(ActionButton("↻ С начала", () =>
        {
            player.Position = TimeSpan.Zero;
            player.Play();
            SetPlaying(true);
        }));

        void SetPlaying(bool value)
        {
            playing = value;
            toggleButton.Content = value ? "Ⅱ Пауза" : "▶ Воспроизвести";
        }

        player.MediaOpened += (_, _) =>
        {
            if (questionPlayer != player) return;
            if (drawing is not null && player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
                drawing.Rect = new Rect(0, 0, player.NaturalVideoWidth, player.NaturalVideoHeight);
            status.Text = fileName;
            controls.IsEnabled = true;
        };
        player.MediaFailed += (_, e) =>
        {
            if (questionPlayer != player) return;
            status.Text = "Не удалось воспроизвести файл: " + e.ErrorException.Message;
            controls.IsEnabled = false;
            SetPlaying(false);
        };
        player.MediaEnded += (_, _) =>
        {
            if (questionPlayer != player) return;
            player.Stop();
            SetPlaying(false);
        };

        audioSource = isAudio ? AudioPlaybackSource.Open(mediaPath) : null;
        string playbackPath = audioSource?.FilePath ?? mediaPath;
        player.Open(new Uri(Path.GetFullPath(playbackPath)));
    }
}
