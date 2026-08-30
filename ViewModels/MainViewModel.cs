using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FairyAI_Android.Models;
using FairyAI_Android.Services;

namespace FairyAI_Android.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILlmService _llm;
    private readonly IIntentAnalyzer _intent;
    private readonly ISearchService _search;
    private readonly ITtsService _tts;
    private readonly IChatHistoryService _history;
    private readonly WakeWordService _wakeWord;
    private readonly string _sessionId;

    [ObservableProperty] private string _userInput = "";
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _statusText = "准备就绪";
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isWakeWordActive;
    [ObservableProperty] private string _wakeWordText = "";

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainViewModel(ILlmService llm, IIntentAnalyzer intent,
        ISearchService search, ITtsService tts, IChatHistoryService history,
        WakeWordService wakeWord)
    {
        _llm = llm;
        _intent = intent;
        _search = search;
        _tts = tts;
        _history = history;
        _wakeWord = wakeWord;
        _sessionId = Guid.NewGuid().ToString("N");

        // Wire wake word events
        _wakeWord.WakeWordDetected += OnWakeWordDetected;
        _wakeWord.PartialResult += OnWakeWordPartial;

        // Auto-start if enabled
        if (_wakeWord.GetConfig().Enabled)
        {
            StartWakeWordListening();
        }
    }

    // === Wake Word ===

    [RelayCommand]
    public void ToggleWakeWord()
    {
        if (_wakeWord.IsRunning)
        {
            _wakeWord.Stop();
            IsWakeWordActive = false;
            StatusText = "唤醒词已关闭";
        }
        else
        {
            StartWakeWordListening();
        }
    }

    private void StartWakeWordListening()
    {
        _wakeWord.LoadConfig();
        if (!_wakeWord.GetConfig().Enabled)
        {
            StatusText = "请在设置中启用唤醒词";
            return;
        }
        _wakeWord.Start();
        IsWakeWordActive = true;
        StatusText = $"聆听唤醒词: {_wakeWord.GetConfig().WakeWord}";
    }

    private async void OnWakeWordDetected(string recognizedText)
    {
        var config = _wakeWord.GetConfig();

        // TTS feedback
        if (config.UseTtsFeedback)
        {
            await _tts.SpeakAsync(config.FeedbackText);
        }

        // Extract command after wake word
        var command = _wakeWord.ExtractCommand(recognizedText);

        if (!string.IsNullOrWhiteSpace(command))
        {
            // Command detected, process directly
            UserInput = command;
            await SendMessageAsync();
        }
        else
        {
            // No command after wake word, listen for command
            StatusText = "请说出你的指令...";

            try
            {
                var asr = new AndroidAsrService();
                var result = await asr.RecognizeAsync();

                if (!string.IsNullOrWhiteSpace(result))
                {
                    UserInput = result;
                    await SendMessageAsync();
                }
                else
                {
                    StatusText = $"未听到指令, 继续监听唤醒词: {config.WakeWord}";
                }
            }
            catch
            {
                StatusText = $"语音识别出错, 继续监听唤醒词: {config.WakeWord}";
            }
        }
    }

    private void OnWakeWordPartial(string text)
    {
        WakeWordText = text;
    }

    // === Messaging ===

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsProcessing) return;

        var text = UserInput;
        UserInput = "";

        var userMsg = new ChatMessage { Role = "user", Content = text };
        Messages.Add(userMsg);
        await _history.SaveAsync(_sessionId, userMsg);

        IsProcessing = true;
        StatusText = "理解中...";

        try
        {
            var historyText = string.Join("\n", Messages.TakeLast(10).Select(m => $"{m.Role}: {m.Content}"));
            var (intent, query, _) = await _intent.AnalyzeAsync(text, historyText);

            StatusText = intent switch
            {
                "open_app" => "打开应用...",
                "search_web" => "联网搜索...",
                "search_file" => "搜索文件...",
                "analyze_screen" => "分析屏幕...",
                _ => "思考中..."
            };

            string response;
            switch (intent)
            {
                case "search_web":
                    var searchResult = await _search.SearchAsync(query);
                    response = await _llm.ChatAsync(
                        Messages.TakeLast(10).ToList(),
                        $"[搜索结果]\n{searchResult}\n\n[用户问题] {text}");
                    break;
                default:
                    response = await StreamResponseAsync(text);
                    break;
            }

            var assistantMsg = new ChatMessage { Role = "assistant", Content = response };
            Messages.Add(assistantMsg);
            await _history.SaveAsync(_sessionId, assistantMsg);

            // TTS response
            await _tts.SpeakAsync(response);

            StatusText = IsWakeWordActive
                ? $"唤醒词监听中: {_wakeWord.GetConfig().WakeWord}"
                : "准备就绪";
        }
        catch (Exception ex)
        {
            StatusText = $"出错: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task<string> StreamResponseAsync(string userMessage)
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            await foreach (var chunk in _llm.StreamChatAsync(Messages.TakeLast(20).ToList(), userMessage))
            {
                sb.Append(chunk);

                var partial = sb.ToString();
                var existing = Messages.LastOrDefault(m => m.Role == "assistant");
                if (existing != null && existing == Messages.LastOrDefault())
                {
                    existing.Content = partial;
                }
                else
                {
                    Messages.Add(new ChatMessage { Role = "assistant", Content = partial });
                }
            }
        }
        catch (Exception ex)
        {
            return $"抱歉，出错了: {ex.Message}";
        }
        return sb.ToString();
    }

    [RelayCommand]
    private async Task StartListeningAsync()
    {
        try
        {
            IsListening = true;
            StatusText = "正在聆听...";

            var asr = new AndroidAsrService();
            var result = await asr.RecognizeAsync();

            if (!string.IsNullOrWhiteSpace(result))
            {
                UserInput = result;
                await SendMessageAsync();
            }
            else
            {
                StatusText = "未识别到语音";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"语音识别出错: {ex.Message}";
        }
        finally
        {
            IsListening = false;
        }
    }

    public void Dispose()
    {
        _wakeWord.WakeWordDetected -= OnWakeWordDetected;
        _wakeWord.PartialResult -= OnWakeWordPartial;
        _wakeWord.Stop();
    }
}
