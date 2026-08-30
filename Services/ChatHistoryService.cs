using System.Text.Json;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

public interface IChatHistoryService
{
    Task InitializeAsync();
    Task SaveAsync(string sessionId, ChatMessage message);
    Task<List<ChatMessage>> GetHistoryAsync(string sessionId, int limit = 50);
    Task<List<ChatMessage>> GetAllSessionsAsync();
}

public class ChatHistoryService : IChatHistoryService
{
    private readonly string _dbPath;

    public ChatHistoryService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "chat_history.json");
    }

    public Task InitializeAsync()
    {
        if (!File.Exists(_dbPath))
            File.WriteAllText(_dbPath, "[]");
        return Task.CompletedTask;
    }

    public async Task SaveAsync(string sessionId, ChatMessage message)
    {
        var history = await LoadAllAsync();
        message.Timestamp = DateTime.Now;
        history.Add(new SessionMessage { SessionId = sessionId, Message = message });
        await SaveAllAsync(history);
    }

    public async Task<List<ChatMessage>> GetHistoryAsync(string sessionId, int limit = 50)
    {
        var history = await LoadAllAsync();
        return history
            .Where(h => h.SessionId == sessionId)
            .Select(h => h.Message)
            .TakeLast(limit)
            .ToList();
    }

    public async Task<List<ChatMessage>> GetAllSessionsAsync()
    {
        var history = await LoadAllAsync();
        return history.Select(h => h.Message).ToList();
    }

    private async Task<List<SessionMessage>> LoadAllAsync()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                var json = await File.ReadAllTextAsync(_dbPath);
                return JsonSerializer.Deserialize<List<SessionMessage>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    private async Task SaveAllAsync(List<SessionMessage> history)
    {
        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_dbPath, json);
    }

    private class SessionMessage
    {
        public string SessionId { get; set; } = "";
        public ChatMessage Message { get; set; } = new();
    }
}
