using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Model Test Service — verifies LLM/TTS endpoints work during setup.
/// </summary>
public class ModelTestService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public record TestResult(bool Success, string Message, long LatencyMs);

    public async Task<TestResult> TestLlmAsync(string baseUrl, string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new TestResult(false, "Base URL 为空", 0);
        if (string.IsNullOrWhiteSpace(model))
            return new TestResult(false, "模型名称为空", 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model = model,
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5
            });

            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var resp = await Http.SendAsync(req);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
                return new TestResult(false, $"HTTP {(int)resp.StatusCode}", sw.ElapsedMilliseconds);

            return new TestResult(true, $"连接成功 (模型: {model})", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new TestResult(false, $"错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    public async Task<TestResult> TestTtsAsync(string baseUrl, string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new TestResult(true, "系统语音无需测试", 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new { model = model, input = "test", voice = "alloy" });
            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var resp = await Http.SendAsync(req);
            sw.Stop();
            return resp.IsSuccessStatusCode
                ? new TestResult(true, "TTS连接成功", sw.ElapsedMilliseconds)
                : new TestResult(false, $"HTTP {(int)resp.StatusCode}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new TestResult(false, $"错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
