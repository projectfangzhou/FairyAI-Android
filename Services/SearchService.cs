using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

public interface ISearchService
{
    Task<string> SearchAsync(string query);
}

public class SearchService : ISearchService
{
    private readonly HttpClient _http = new();

    public async Task<string> SearchAsync(string query)
    {
        var config = ConfigManager.Load();

        // Use Tavily if configured
        if (!string.IsNullOrWhiteSpace(config.LLM.ApiKey))
        {
            try
            {
                var url = $"https://api.tavily.com/search";
                var body = new { api_key = config.LLM.ApiKey, query, max_results = 5 };
                var json = JsonSerializer.Serialize(body);
                var response = await _http.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
                var result = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(result);

                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var item in results.EnumerateArray().Take(5))
                    {
                        var title = item.TryGetProperty("title", out var t) ? t.GetString() : "";
                        var content = item.TryGetProperty("content", out var c) ? c.GetString() : "";
                        sb.AppendLine($"**{title}**");
                        sb.AppendLine(content);
                        sb.AppendLine();
                    }
                    return sb.ToString();
                }
            }
            catch { }
        }

        // Fallback: use LLM to answer
        var messages = new List<Models.ChatMessage>
        {
            new() { Role = "system", Content = "你是一个有帮助的助手。请回答用户的问题。" },
            new() { Role = "user", Content = query }
        };
        return await Task.FromResult($"暂无搜索结果。请确认已配置搜索 API。");
    }
}
