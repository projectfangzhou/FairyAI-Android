// TranslateService — real LLM-based translation
// Android implementation

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>Translation service using LLM API.</summary>
public class TranslateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<string> TranslateAsync(string text, string targetLanguage = "en")
    {
        try
        {
            var config = ConfigManager.Load();
            var llm = config.LLM;
            var prompt = $"Translate to {targetLanguage}: {text}";

            var payload = JsonSerializer.Serialize(new
            {
                model = llm.Model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 500
            });

            var req = new HttpRequestMessage(HttpMethod.Post, llm.BaseUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(llm.ApiKey))
                req.Headers.Add("Authorization", $"Bearer {llm.ApiKey}");

            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? text;
        }
        catch
        {
            return text;
        }
    }
}
