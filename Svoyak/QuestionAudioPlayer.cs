using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Svoyak;

internal sealed class QuestionAudioPlayer : IDisposable
{
    private const string AudioHost = "svoyak-audio.test";
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"SvoyakAudio-{Guid.NewGuid():N}");
    private readonly CancellationTokenSource lifetime = new();

    public WebView2 View { get; } = new() { Width = 1, Height = 1, Focusable = false, IsHitTestVisible = false };

    public async Task OpenAsync(string path, Action onReady, Action<bool> onPlayingChanged, Action<string> onError)
    {
        var cancellation = lifetime.Token;
        try
        {
            Directory.CreateDirectory(folder);
            await using var input = File.OpenRead(path);
            var header = new byte[8];
            int size = await input.ReadAtLeastAsync(header, header.Length, false, cancellation);
            input.Position = 0;
            // Some downloaded AAC tracks have an .mp3 extension despite using an MP4 container.
            string extension = size == 8 && header.AsSpan(4).SequenceEqual("ftyp"u8) ? ".m4a"
                : Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) ? ".wav" : ".mp3";
            string name = "track" + extension;
            await using (var output = File.Create(Path.Combine(folder, name)))
                await input.CopyToAsync(output, cancellation);

            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Svoyak", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile, options: new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
            cancellation.ThrowIfCancellationRequested();
            await View.EnsureCoreWebView2Async(environment);
            cancellation.ThrowIfCancellationRequested();

            var browser = View.CoreWebView2;
            browser.SetVirtualHostNameToFolderMapping(AudioHost, folder, CoreWebView2HostResourceAccessKind.DenyCors);
            browser.Settings.AreDevToolsEnabled = false;
            browser.Settings.AreDefaultContextMenusEnabled = false;
            browser.WebMessageReceived += (_, e) =>
            {
                if (cancellation.IsCancellationRequested) return;
                string message = e.TryGetWebMessageAsString();
                if (message == "ready") onReady();
                else if (message == "playing") onPlayingChanged(true);
                else if (message == "paused") onPlayingChanged(false);
                else onError(message);
            };
            View.NavigateToString($$"""
                <audio id="track" preload="auto"></audio>
                <script>
                const track = document.getElementById('track');
                const send = message => chrome.webview.postMessage(message);
                track.addEventListener('canplay', () => send('ready'), { once: true });
                track.addEventListener('error', () => send(track.error.message || 'Windows не смогла прочитать аудио.'));
                track.addEventListener('play', () => send('playing'));
                track.addEventListener('pause', () => send('paused'));
                track.addEventListener('ended', () => send('paused'));
                track.src = 'https://{{AudioHost}}/{{name}}';
                function play(restart) {
                    if (restart || track.ended) track.currentTime = 0;
                    track.play().catch(error => {
                        if (error.name !== 'AbortError') send(error.message);
                    });
                }
                </script>
                """);
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested)
        {
            onError(error.Message);
        }
        catch (Exception) when (cancellation.IsCancellationRequested)
        {
            DeleteTrackFolder();
        }
    }

    public Task ToggleAsync() => View.ExecuteScriptAsync("track.paused || track.ended ? play(false) : track.pause()");
    public Task RestartAsync() => View.ExecuteScriptAsync("play(true)");

    public void Dispose()
    {
        lifetime.Cancel();
        View.Dispose();
        lifetime.Dispose();
        DeleteTrackFolder();
    }

    private void DeleteTrackFolder()
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
