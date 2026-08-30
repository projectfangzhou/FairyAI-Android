using Android.Content;
using Android.Speech;
using Android.OS;

namespace FairyAI_Android.Services;

/// <summary>
/// Android platform-specific ASR service using Android SpeechRecognizer.
/// Runs on the main thread as required by Android.
/// </summary>
public class AndroidAsrService : Java.Lang.Object, IRecognitionListener
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private Android.Speech.SpeechRecognizer? _recognizer;
    private CancellationTokenSource? _cts;

    public Task<string?> RecognizeAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                _recognizer = Android.Speech.SpeechRecognizer.CreateSpeechRecognizer(context);

                if (_recognizer == null)
                {
                    _tcs.TrySetResult(null);
                    return;
                }

                _recognizer.SetRecognitionListener(this);

                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Chinese);
                intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);

                _recognizer.StartListening(intent);

                // Timeout after 30 seconds
                _ = Task.Delay(30000, _cts.Token).ContinueWith(_ =>
                {
                    Stop();
                    _tcs.TrySetResult(null);
                });
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        });

        return _tcs.Task;
    }

    public void Stop()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { _recognizer?.StopListening(); } catch { }
            try { _recognizer?.Destroy(); } catch { }
            _recognizer = null;
        });
    }

    // IRecognitionListener implementations
    public void OnReadyForSpeech(Bundle? @params) { }
    public void OnBeginningOfSpeech() { }
    public void OnRmsChanged(float rmsdB) { }
    public void OnBufferReceived(byte[]? buffer) { }
    public void OnEndOfSpeech()
    {
        Stop();
        _tcs.TrySetResult(null);
    }

    public void OnError(SpeechRecognizerError error)
    {
        Stop();
        _tcs.TrySetResult(null);
    }

    public void OnResults(Bundle? results)
    {
        var matches = results?.GetStringArrayList("android.speech.extra.RESULTS");
        var text = matches?.FirstOrDefault();
        Stop();
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
