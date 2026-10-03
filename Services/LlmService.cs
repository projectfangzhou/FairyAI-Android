using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FairyAI_Android.Services;

public interface ILlmService
{
    IAsyncEnumerable<string> StreamChatAsync(List<Models.ChatMessage> history, string userMessage);
    Task<string> ChatAsync(List<Models.ChatMessage> history, string userMessage);
}

public class LlmService : ILlmService
{
    private readonly HttpClient _http = new();
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public async Task<string> ChatAsync(List<Models.ChatMessage> history, string userMessage)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in StreamChatAsync(history, userMessage))
            sb.Append(chunk);
        return sb.ToString();
    }

    public async IAsyncEnumerable<string> StreamChatAsync(List<Models.ChatMessage> history, string userMessage)
    {
        var appConfig = ConfigManager.Load();
        var config = appConfig.LLM;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            yield return "Please configure LLM API Key in settings";
            yield break;
        }

        var messages = new List<object>();

        // Inject personality system prompt
        var personality = appConfig.Personality;
        if (!string.IsNullOrWhiteSpace(personality.SystemPrompt))
        {
            messages.Add(new { role = "system", content = personality.SystemPrompt });
        }

        // Add conversation history
        foreach (var m in history.TakeLast(20))
            messages.Add(new { role = m.Role, content = m.Content });

        messages.Add(new { role = "user", content = userMessage });

        var body = new { model = config.Model, messages, stream = true, max_tokens = 2048 };
        var json = JsonSerializer.Serialize(body);

        HttpResponseMessage? response = null;
        bool success = false;

        // Try primary
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, config.BaseUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            success = true;
        }
        catch (Exception ex)
        {
            Log($"LLM primary error: {ex.Message}");
        }

        // Try fallback
        if (!success)
        {
            var fallback = ConfigManager.Load().FallbackLLM;
            if (!string.IsNullOrWhiteSpace(fallback.ApiKey))
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, fallback.BaseUrl)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("Authorization", $"Bearer {fallback.ApiKey}");
                    response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    success = true;
                }
                catch (Exception ex)
                {
                    Log($"LLM fallback error: {ex.Message}");
                }
            }
        }

        if (!success || response == null)
        {
            yield return "LLM request failed";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            string? yieldText = null;
            try
            {
                var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var content))
                    {
                        yieldText = content.GetString();
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(yieldText))
                yield return yieldText;
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [LLM] {msg}\n"); } catch { }
    }
}

