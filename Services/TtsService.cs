namespace FairyAI_Android.Services;

public interface ITtsService
{
    Task SpeakAsync(string text);
    void Stop();
}

public class TtsService : ITtsService
{
    private CancellationTokenSource? _cts;
    private readonly ITtsApiService _apiTts;

    public TtsService(ITtsApiService apiTts)
    {
        _apiTts = apiTts;
    }

    public async Task SpeakAsync(string text)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        // Try API TTS first
        if (_apiTts.IsAvailable)
        {
            try
            {
                var audioBytes = await _apiTts.SynthesizeAsync(text);
                if (audioBytes.Length > 0)
                {
                    await PlayAudioAsync(audioBytes, _cts.Token);
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API TTS failed: {ex.Message}");
            }
        }

        // Fallback to Android system TTS
        await SystemTtsAsync(text, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task PlayAudioAsync(byte[] audioData, CancellationToken ct)
    {
        try
        {
            // Write to temp file and play with Android MediaPlayer
            var tempFile = Path.Combine(FileSystem.CacheDirectory, $"tts_{Guid.NewGuid():N}.mp3");
            await File.WriteAllBytesAsync(tempFile, audioData, ct);

            var javaFile = new Java.IO.File(tempFile);
            var uri = Android.Net.Uri.FromFile(javaFile);

            var tcs = new TaskCompletionSource<bool>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var player = new Android.Media.MediaPlayer();
                    player.Completion += (_, _) =>
                    {
                        tcs.TrySetResult(true);
                        player.Release();
                        try { File.Delete(tempFile); } catch { }
                    };
                    player.Error += (_, e) =>
                    {
                        tcs.TrySetResult(false);
                        player.Release();
                    };

                    player.SetDataSource(Android.App.Application.Context, uri);
                    player.Prepare();
                    player.Start();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            await tcs.Task.WaitAsync(ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlayAudio error: {ex.Message}");
        }
    }

    private async Task SystemTtsAsync(string text, CancellationToken ct)
    {
        try
        {
            var locales = await TextToSpeech.GetLocalesAsync();
            var zhLocale = locales.FirstOrDefault(l => l.Language.Contains("zh"));

            var settings = new SpeechOptions
            {
                Rate = 1.0f,
                Pitch = 1.0f,
                Locale = zhLocale
            };

            await TextToSpeech.SpeakAsync(text, settings, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"System TTS error: {ex.Message}");
        }
    }
}
