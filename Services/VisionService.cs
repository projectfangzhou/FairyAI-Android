// Source: bilibili_learning_bot + PC implementations
// Android Vision Service — screen analysis with cloud API (no accessibility needed)

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Vision Service — screen/image analysis using cloud vision API.
/// Works without accessibility permission (unlike screen automation).
/// </summary>
public class VisionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Analyze image with vision model.</summary>
    public async Task<string> AnalyzeImageAsync(string prompt, string base64Image)
    {
        try
        {
            var config = ConfigManager.Load();
            var vision = config.Vision;

            var payload = JsonSerializer.Serialize(new
            {
                model = vision.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } }
                        }
                    }
                },
                max_tokens = 1000
            });

            var req = new HttpRequestMessage(HttpMethod.Post, vision.BaseUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(vision.ApiKey))
                req.Headers.Add("Authorization", $"Bearer {vision.ApiKey}");

            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            Log($"Vision error: {ex.Message}");
            return $"分析失败: {ex.Message}";
        }
    }

    /// <summary>Capture and analyze screen.</summary>
    public async Task<string> AnalyzeScreenAsync(string prompt = "描述屏幕内容")
    {
        try
        {
            // Capture screenshot
            var screenshot = await CaptureScreenAsync();
            if (screenshot == null) return "截屏失败";

            var base64 = Convert.ToBase64String(screenshot);
            return await AnalyzeImageAsync(prompt, base64);
        }
        catch (Exception ex)
        {
            Log($"Screen analysis error: {ex.Message}");
            return $"错误: {ex.Message}";
        }
    }

    /// <summary>Capture screen screenshot.</summary>
    public async Task<byte[]?> CaptureScreenAsync()
    {
        try
        {
            var screenshot = await Screenshot.CaptureAsync();
            if (screenshot == null) return null;

            using var stream = await screenshot.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Log($"Capture error: {ex.Message}");
            return null;
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [VISION] {msg}\n"); } catch { }
    }
}

/// <summary>
/// Multimodal model service — supports text+vision in one model.
/// </summary>
public class MultimodalService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Chat with multimodal model (text + optional image).</summary>
    public async Task<string> ChatAsync(string message, string? base64Image = null)
    {
        try
        {
            var config = ConfigManager.Load();
            var custom = config.CustomPlatform;

            if (!custom.MultimodalEnabled)
                return "未配置多模态模型";

            var content = new List<object>();
            content.Add(new { type = "text", text = message });
            if (base64Image != null)
                content.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } });

            var payload = JsonSerializer.Serialize(new
            {
                model = custom.MultimodalModel,
                messages = new[] { new { role = "user", content = content.ToArray() } },
                max_tokens = 1000
            });

            var req = new HttpRequestMessage(HttpMethod.Post, custom.MultimodalBaseUrl)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(custom.MultimodalApiKey))
                req.Headers.Add("Authorization", $"Bearer {custom.MultimodalApiKey}");

            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            return $"错误: {ex.Message}";
        }
    }
}
