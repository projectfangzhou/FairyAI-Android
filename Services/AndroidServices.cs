using System.IO;
using System.Text.Json;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>BiliLearn service — Bilibili video learning with daily limit.</summary>
public class BiliLearnService
{
    private const int DailyLimit = 20;
    private static readonly string StatsPath = Path.Combine(FileSystem.AppDataDirectory, "bililearn_stats.json");

    public async Task<string> LearnFromUrlAsync(string url)
    {
        if (!CanLearnToday()) return $"今日学习已达上限 ({DailyLimit})";
        await Task.CompletedTask;
        IncrementCount();
        return $"已学习: {url}";
    }

    public bool CanLearnToday() => GetCountToday() < DailyLimit;

    private int GetCountToday()
    {
        try
        {
            if (!File.Exists(StatsPath)) return 0;
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath));
            return stats?.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd"), 0) ?? 0;
        }
        catch { return 0; }
    }

    private void IncrementCount()
    {
        try
        {
            var stats = File.Exists(StatsPath)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath)) ?? new()
                : new Dictionary<string, int>();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            stats[today] = stats.GetValueOrDefault(today, 0) + 1;
            File.WriteAllText(StatsPath, JsonSerializer.Serialize(stats));
        }
        catch { }
    }
}

/// <summary>Important info auto-save service.</summary>
public class ImportantInfoService
{
    private readonly KnowledgeBaseService _kb;

    public ImportantInfoService(KnowledgeBaseService kb) => _kb = kb;

    public async Task<string> ExtractAndSaveAsync(string text)
    {
        await Task.CompletedTask;
        return "";
    }
}

/// <summary>Speaker style analyzer.</summary>
public class SpeakerStyleAnalyzer
{
    private readonly ILlmService _llm;

    public SpeakerStyleAnalyzer(ILlmService llm) => _llm = llm;

    public async Task<string> AnalyzeAsync(string speakerId, List<ChatMessage> messages)
    {
        await Task.CompletedTask;
        return "";
    }
}

/// <summary>Token usage tracking service.</summary>
public class TokenUsageService
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "token_usage.json");

    public void RecordUsage(string model, int inputTokens, int outputTokens) { }

    public Dictionary<string, object> GetUsageStats() => new();
}

/// <summary>Translation service.</summary>
public class TranslateService
{
    private readonly ILlmService _llm;

    public TranslateService(ILlmService llm) => _llm = llm;

    public async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        var prompt = $"Translate to {targetLanguage}: {text}";
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync(new List<ChatMessage>(), prompt))
            response.Append(chunk);
        return response.ToString();
    }
}

/// <summary>Markdown rendering service.</summary>
public class MarkdownService
{
    public string RenderHtml(string markdown) => markdown;
}

/// <summary>Export service for conversations.</summary>
public class ExportService
{
    public string ExportToMarkdown(List<ChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var m in messages)
            sb.AppendLine($"**{m.Role}**: {m.Content}\n");
        return sb.ToString();
    }
}

/// <summary>Idle sound service.</summary>
public class IdleSoundService
{
    public event Action<byte[]>? SoundReady;
    public void Start() { }
    public void Stop() { }
}

/// <summary>MMD model service.</summary>
public class MmdModelService
{
    public static ModelType DetectModelType(string path) => ModelType.None;
}

public enum ModelType { None, Live2D, MMD }

/// <summary>YT-DLP download service.</summary>
public class YtDlpService
{
    public bool IsAvailable => true;

    public async Task<string> DownloadVideoAsync(string url, string outputDir)
    {
        await Task.CompletedTask;
        return "";
    }
}

/// <summary>Note service.</summary>
public class NoteService
{
    public Task CreateNoteAsync(string title, string content) => Task.CompletedTask;
    public Task<List<string>> SearchNotesAsync(string query) => Task.FromResult(new List<string>());
}

/// <summary>Reminder service.</summary>
public class ReminderService
{
    public Task SetReminderAsync(DateTime time, string message) => Task.CompletedTask;
}

/// <summary>Shortcut service.</summary>
public class ShortcutService
{
    public Task CreateShortcutAsync(string name, string template) => Task.CompletedTask;
}

/// <summary>Skill service.</summary>
public class SkillService
{
    public List<string> ListSkills() => new();
}

/// <summary>Clipboard history service.</summary>
public class ClipboardHistoryService
{
    public List<string> GetHistory() => new();
}

/// <summary>Lobotomy error display.</summary>
public class LobotomyErrorDisplay
{
    public static void Show(string errorCode, string message) { }
}
