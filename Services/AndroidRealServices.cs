// NoteService — real SQLite-based note storage
// Ported from PC MyAiAssistant/Services/NoteService.cs

using System.IO;
using System.Text.Json;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>Note service with file-based storage.</summary>
public class NoteService
{
    private readonly string _notesDir;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public NoteService()
    {
        _notesDir = Path.Combine(FileSystem.AppDataDirectory, "notes");
        Directory.CreateDirectory(_notesDir);
    }

    /// <summary>Create a note.</summary>
    public async Task<string> CreateNoteAsync(string title, string content)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var note = new
        {
            id = id,
            title = title,
            content = content,
            created = DateTime.UtcNow.ToString("o"),
            tags = new List<string>()
        };
        var path = Path.Combine(_notesDir, $"{id}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(note, new JsonSerializerOptions { WriteIndented = true }));
        Log($"Note created: {title}");
        return id;
    }

    /// <summary>Search notes by keyword.</summary>
    public async Task<List<Dictionary<string, object>>> SearchNotesAsync(string query)
    {
        var results = new List<Dictionary<string, object>>();
        foreach (var file in Directory.GetFiles(_notesDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);
                var title = doc.RootElement.GetProperty("title").GetString() ?? "";
                var content = doc.RootElement.GetProperty("content").GetString() ?? "";
                if (title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new Dictionary<string, object>
                    {
                        ["id"] = doc.RootElement.GetProperty("id").GetString() ?? "",
                        ["title"] = title,
                        ["content"] = content[..Math.Min(200, content.Length)]
                    });
                }
            }
            catch { }
        }
        return results;
    }

    /// <summary>List all notes.</summary>
    public async Task<List<Dictionary<string, object>>> ListNotesAsync()
    {
        var results = new List<Dictionary<string, object>>();
        foreach (var file in Directory.GetFiles(_notesDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);
                results.Add(new Dictionary<string, object>
                {
                    ["id"] = doc.RootElement.GetProperty("id").GetString() ?? "",
                    ["title"] = doc.RootElement.GetProperty("title").GetString() ?? "",
                    ["created"] = doc.RootElement.GetProperty("created").GetString() ?? ""
                });
            }
            catch { }
        }
        return results;
    }

    /// <summary>Delete a note.</summary>
    public async Task<bool> DeleteNoteAsync(string id)
    {
        var path = Path.Combine(_notesDir, $"{id}.json");
        if (File.Exists(path))
        {
            await Task.Run(() => File.Delete(path));
            return true;
        }
        return false;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [NOTE] {msg}\n"); } catch { }
    }
}

/// <summary>Reminder service with file-based storage.</summary>
public class ReminderService
{
    private readonly string _remindersPath;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public ReminderService()
    {
        _remindersPath = Path.Combine(FileSystem.AppDataDirectory, "reminders.json");
    }

    /// <summary>Set a reminder.</summary>
    public async Task<string> SetReminderAsync(DateTime time, string message)
    {
        var reminders = await LoadRemindersAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        reminders.Add(new Dictionary<string, object>
        {
            ["id"] = id,
            ["time"] = time.ToString("o"),
            ["message"] = message,
            ["triggered"] = false
        });
        await SaveRemindersAsync(reminders);
        Log($"Reminder set: {message} at {time}");
        return id;
    }

    /// <summary>List all reminders.</summary>
    public async Task<List<Dictionary<string, object>>> ListRemindersAsync()
    {
        return await LoadRemindersAsync();
    }

    /// <summary>Cancel a reminder.</summary>
    public async Task<bool> CancelReminderAsync(string id)
    {
        var reminders = await LoadRemindersAsync();
        var before = reminders.Count;
        reminders.RemoveAll(r => r.ContainsKey("id") && r["id"]?.ToString() == id);
        await SaveRemindersAsync(reminders);
        return reminders.Count < before;
    }

    private async Task<List<Dictionary<string, object>>> LoadRemindersAsync()
    {
        try
        {
            if (File.Exists(_remindersPath))
            {
                var json = await File.ReadAllTextAsync(_remindersPath);
                return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? new();
            }
        }
        catch { }
        return new List<Dictionary<string, object>>();
    }

    private async Task SaveRemindersAsync(List<Dictionary<string, object>> reminders)
    {
        await File.WriteAllTextAsync(_remindersPath,
            JsonSerializer.Serialize(reminders, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [REMIND] {msg}\n"); } catch { }
    }
}

/// <summary>Token usage tracking service.</summary>
public class TokenUsageService
{
    private readonly string _usagePath;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public TokenUsageService()
    {
        _usagePath = Path.Combine(FileSystem.AppDataDirectory, "token_usage.json");
    }

    /// <summary>Record token usage.</summary>
    public async Task RecordUsageAsync(string model, int inputTokens, int outputTokens)
    {
        var usage = await LoadUsageAsync();
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (!usage.ContainsKey(today))
            usage[today] = new Dictionary<string, object>();

        var dayData = usage[today] as Dictionary<string, object>;
        if (dayData == null) return;

        if (!dayData.ContainsKey(model))
            dayData[model] = new Dictionary<string, int> { ["input"] = 0, ["output"] = 0, ["total"] = 0 };

        var modelData = dayData[model] as Dictionary<string, int>;
        if (modelData == null) return;

        modelData["input"] += inputTokens;
        modelData["output"] += outputTokens;
        modelData["total"] += inputTokens + outputTokens;

        await SaveUsageAsync(usage);
    }

    /// <summary>Get usage statistics.</summary>
    public async Task<Dictionary<string, object>> GetUsageStatsAsync()
    {
        var usage = await LoadUsageAsync();
        return new Dictionary<string, object>
        {
            ["daily"] = usage,
            ["totalDays"] = usage.Count
        };
    }

    private async Task<Dictionary<string, Dictionary<string, object>>> LoadUsageAsync()
    {
        try
        {
            if (File.Exists(_usagePath))
            {
                var json = await File.ReadAllTextAsync(_usagePath);
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json) ?? new();
            }
        }
        catch { }
        return new Dictionary<string, Dictionary<string, object>>();
    }

    private async Task SaveUsageAsync(Dictionary<string, Dictionary<string, object>> usage)
    {
        await File.WriteAllTextAsync(_usagePath,
            JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Important info extraction service — regex-based date/address extraction.</summary>
public class ImportantInfoService
{
    private readonly KnowledgeBaseService _kb;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public ImportantInfoService(KnowledgeBaseService kb) => _kb = kb;

    /// <summary>Extract and save important info (dates, addresses, phone numbers).</summary>
    public async Task<string> ExtractAndSaveAsync(string text)
    {
        var extracted = new List<string>();

        // Extract dates
        var datePattern = @"(\d{4}[-/年]\d{1,2}[-/月]\d{1,2}[日]?)|(\d{1,2}[-/月]\d{1,2}[日]?)|((今天|明天|后天|昨天|前天|周[一二三四五六日天]|星期[一二三四五六日天])[\s]*(上午|下午|晚上|中午)?)";
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, datePattern))
            extracted.Add($"日期: {m.Value}");

        // Extract phone numbers
        var phonePattern = @"(1[3-9]\d{9})|(\d{3,4}-\d{7,8})";
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, phonePattern))
            extracted.Add($"电话: {m.Value}");

        // Extract addresses
        var addrPattern = @"([一-龥]+(省|市|区|县|镇|街|路|号|栋|室))";
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, addrPattern))
            extracted.Add($"地址: {m.Value}");

        if (extracted.Count > 0)
        {
            var content = string.Join("\n", extracted);
            await _kb.SaveImportantInfoAsync("auto_extracted", content);
            Log($"Extracted {extracted.Count} items");
            return content;
        }

        return "未找到重要信息";
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [INFO] {msg}\n"); } catch { }
    }
}
