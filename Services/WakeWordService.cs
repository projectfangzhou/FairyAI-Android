using Android.Content;
using Android.OS;
using Android.Speech;

namespace FairyAI_Android.Services;

/// <summary>
/// Configuration for wake word detection.
/// </summary>
public class WakeWordConfig
{
    public bool Enabled { get; set; } = false;
    public string WakeWord { get; set; } = "小精灵";
    public string[] AlternateWords { get; set; } = Array.Empty<string>();
    public bool UseTtsFeedback { get; set; } = true;
    public string FeedbackText { get; set; } = "我在";
    public int ListenTimeoutSeconds { get; set; } = 10;
    public int CooldownMs { get; set; } = 2000;
}

/// <summary>
/// Continuous background wake word listener.
/// 
/// Architecture:
///   1. Runs a persistent recognition loop (restarts after each result/timeout)
///   2. Checks partial/final results for the configured wake word
///   3. When detected → fires WakeWordDetected event → command mode activates
///   4. Optional TTS feedback ("我在")
/// 
/// Requires: FOREGROUND_SERVICE + RECORD_AUDIO permissions.
/// Uses Android SpeechRecognizer in continuous loop mode.
/// </summary>
public class WakeWordService : IDisposable
{
    private readonly List<string> _wakeWords = new();
    private Android.Speech.SpeechRecognizer? _recognizer;
    private CancellationTokenSource? _loopCts;
    private bool _isActive;
    private bool _inCooldown;
    private DateTime _lastDetection = DateTime.MinValue;
    private readonly string _logPath;
    private WakeWordConfig _config = new();

    public bool IsRunning => _isActive;
    public event Action<string>? WakeWordDetected; // fires with the recognized text containing wake word
    public event Action<string>? PartialResult;    // fires for UI display of partial results

    public WakeWordService()
    {
        _logPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");
        LoadConfig();
    }

    public void LoadConfig()
    {
        var appConfig = ConfigManager.Load();
        // WakeWordConfig stored in PersonalityConfig or as a separate section
        // For now, use hardcoded defaults + config file
        try
        {
            var configPath = Path.Combine(FileSystem.AppDataDirectory, "wake_word.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _config = System.Text.Json.JsonSerializer.Deserialize<WakeWordConfig>(json) ?? new WakeWordConfig();
            }
        }
        catch { }

        _wakeWords.Clear();
        _wakeWords.Add(_config.WakeWord);
        if (_config.AlternateWords != null)
            _wakeWords.AddRange(_config.AlternateWords);

        Log($"Wake words loaded: {string.Join(", ", _wakeWords)}");
    }

    public void SaveConfig(WakeWordConfig config)
    {
        _config = config;
        _wakeWords.Clear();
        _wakeWords.Add(config.WakeWord);
        if (config.AlternateWords != null)
            _wakeWords.AddRange(config.AlternateWords);

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(config);
            File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "wake_word.json"), json);
        }
        catch { }
        Log($"Wake config saved, words: {string.Join(", ", _wakeWords)}");
    }

    public WakeWordConfig GetConfig() => _config;

    /// <summary>Start the continuous wake word listening loop.</summary>
    public void Start()
    {
        if (_isActive || !_config.Enabled) return;
        _isActive = true;
        _loopCts = new CancellationTokenSource();
        Log("Wake word service started");
        _ = ListeningLoopAsync(_loopCts.Token);
    }

    /// <summary>Stop the wake word listener.</summary>
    public void Stop()
    {
        _isActive = false;
        _loopCts?.Cancel();
        _loopCts = null;
        DestroyRecognizer();
        Log("Wake word service stopped");
    }

    /// <summary>Check if a text contains any of the configured wake words.</summary>
    public bool ContainsWakeWord(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return _wakeWords.Any(w => lower.Contains(w.ToLowerInvariant()));
    }

    /// <summary>Extract the command part after the wake word.</summary>
    public string ExtractCommand(string fullText)
    {
        var lower = fullText.ToLowerInvariant();
        foreach (var word in _wakeWords)
        {
            var idx = lower.IndexOf(word.ToLowerInvariant());
            if (idx >= 0)
            {
                var after = fullText[(idx + word.Length)..].Trim();
                // Remove common filler words
                after = after.TrimStart('，', ',', '。', '.', ' ', '，');
                return after;
            }
        }
        return fullText;
    }

    private async Task ListeningLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isActive)
        {
            if (_inCooldown)
            {
                await Task.Delay(500, ct).ContinueWith(_ => { });
                continue;
            }

            try
            {
                var result = await RecognizeOnceAsync(ct);
                if (string.IsNullOrEmpty(result))
                {
                    // Timeout or no result, loop again
                    await Task.Delay(200, ct).ContinueWith(_ => { });
                    continue;
                }

                PartialResult?.Invoke(result);

                if (ContainsWakeWord(result))
                {
                    var now = DateTime.Now;
                    if ((now - _lastDetection).TotalMilliseconds > _config.CooldownMs)
                    {
                        _lastDetection = now;
                        _inCooldown = true;

                        Log($"Wake word detected: {result}");
                        WakeWordDetected?.Invoke(result);

                        // Brief cooldown to avoid double triggers
                        await Task.Delay(_config.CooldownMs, ct).ContinueWith(_ => { });
                        _inCooldown = false;
                    }
                }
            }
            catch (System.OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log($"Wake loop error: {ex.Message}");
                await Task.Delay(2000, ct).ContinueWith(_ => { });
            }
        }
    }

    private Task<string?> RecognizeOnceAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string?>();

        MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                _recognizer?.Destroy();
                _recognizer = Android.Speech.SpeechRecognizer.CreateSpeechRecognizer(context);

                if (_recognizer == null)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                var listener = new WakeWordRecognitionListener(tcs);
                _recognizer.SetRecognitionListener(listener);

                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Chinese);
                intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 1500L);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 1000L);

                _recognizer.StartListening(intent);

                // Timeout
                _ = Task.Delay(_config.ListenTimeoutSeconds * 1000, ct).ContinueWith(_ =>
                {
                    StopRecognizer();
                    tcs.TrySetResult(null);
                }, ct);
            }
            catch (Exception ex)
            {
                Log($"RecognizeOnce error: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });

        return tcs.Task;
    }

    private void StopRecognizer()
    {
        try { _recognizer?.StopListening(); } catch { }
    }

    private void DestroyRecognizer()
    {
        try { _recognizer?.Destroy(); } catch { }
        _recognizer = null;
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [WakeWord] {msg}\n"); } catch { }
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Internal recognition listener that bridges results back to the TaskCompletionSource.
/// </summary>
internal class WakeWordRecognitionListener : Java.Lang.Object, IRecognitionListener
{
    private readonly TaskCompletionSource<string?> _tcs;

    public WakeWordRecognitionListener(TaskCompletionSource<string?> tcs) => _tcs = tcs;

    public void OnReadyForSpeech(Bundle? @params) { }
    public void OnBeginningOfSpeech() { }
    public void OnRmsChanged(float rmsdB) { }
    public void OnBufferReceived(byte[]? buffer) { }

    public void OnEndOfSpeech() { }

    public void OnError(SpeechRecognizerError error)
    {
        _tcs.TrySetResult(null);
    }

    public void OnResults(Bundle? results)
    {
        var matches = results?.GetStringArrayList("android.speech.extra.RESULTS");
        var text = matches?.FirstOrDefault();
        _tcs.TrySetResult(text);
    }

    public void OnPartialResults(Bundle? partialResults)
    {
        var matches = partialResults?.GetStringArrayList("android.speech.extra.RESULTS");
        var text = matches?.FirstOrDefault();
        if (!string.IsNullOrEmpty(text))
        {
            _tcs.TrySetResult(text);
        }
    }

    public void OnEvent(int eventType, Bundle? @params) { }
}
