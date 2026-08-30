using System.Text.Json;

namespace FairyAI_Android.Services;

public interface IIntentAnalyzer
{
    Task<(string Intent, string Query, string Reply)> AnalyzeAsync(string userMessage, string history);
}

public class IntentAnalyzer : IIntentAnalyzer
{
    private readonly ILlmService _llm;

    public IntentAnalyzer(ILlmService llm)
    {
        _llm = llm;
    }

    public async Task<(string Intent, string Query, string Reply)> AnalyzeAsync(string userMessage, string history)
    {
        var systemPrompt = "You are an intent analyzer. Return JSON: {\"intent\":\"type\",\"query\":\"keyword\",\"reply\":\"short\"}. " +
            "Types: chat, open_app, search_web, search_file, analyze_screen, open_song. Only return JSON.";

        var messages = new List<Models.ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userMessage }
        };

        var response = await _llm.ChatAsync(messages, "");

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var doc = JsonDocument.Parse(json);
                var intent = doc.RootElement.GetProperty("intent").GetString() ?? "chat";
                var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                var reply = doc.RootElement.TryGetProperty("reply", out var r) ? r.GetString() ?? "" : "";
                return (intent, query, reply);
            }
        }
        catch { }

        return ("chat", userMessage, "");
    }
}
