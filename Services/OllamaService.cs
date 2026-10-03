// OllamaService — real implementation with local Ollama API calls
// Ported from PC MyAiAssistant/Services/OllamaService.cs

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Ollama local model service — real implementation with API calls.
/// </summary>
public class OllamaService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Check if Ollama is running locally.</summary>
    public async Task<bool> IsRunningAsync()
    {
        try
        {
            var resp = await Http.GetAsync("http://localhost:11434/api/tags");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>List available local models.</summary>
    public async Task<List<string>> ListModelsAsync()
    {
        try
        {
            var resp = await Http.GetAsync("http://localhost:11434/api/tags");
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelsEl))
                foreach (var m in modelsEl.EnumerateArray())
                    models.Add(m.GetProperty("name").GetString() ?? "");
            return models;
        }
        catch { return new List<string>(); }
    }

    /// <summary>Generate text with local Ollama model.</summary>
    public async Task<string> GenerateAsync(string model, string prompt)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { model = model, prompt = prompt, stream = false });
            var resp = await Http.PostAsync("http://localhost:11434/api/generate",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("response").GetString() ?? "";
        }
        catch (Exception ex)
        {
            Log($"Ollama generate error: {ex.Message}");
            return "";
        }
    }

    /// <summary>Chat with local Ollama model.</summary>
    public async Task<string> ChatAsync(string model, string message)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model = model,
                messages = new[] { new { role = "user", content = message } },
                stream = false
            });
            var resp = await Http.PostAsync("http://localhost:11434/api/chat",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            Log($"Ollama chat error: {ex.Message}");
            return "";
        }
    }

    /// <summary>Pull a model from Ollama registry.</summary>
    public async Task<bool> PullModelAsync(string model)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { name = model });
            var resp = await Http.PostAsync("http://localhost:11434/api/pull",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [OLLAMA] {msg}\n"); } catch { }
    }
}
