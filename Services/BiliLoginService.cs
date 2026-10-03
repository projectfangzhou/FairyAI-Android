// Source: https://github.com/xiaoyaya191/bilibili_learning_bot.git
// B站二维码登录实现，参照开源项目 bilibili_learning_bot
// API: passport.bilibili.com/x/passport-login/web/qrcode/generate
//      passport.bilibili.com/x/passport-login/web/qrcode/poll

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Bilibili QR Login Service — real QR code generation and login polling.
/// Source: https://github.com/xiaoyaya191/bilibili_learning_bot.git
/// </summary>
public class BiliLoginService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Generate QR code URL from Bilibili passport API.</summary>
    public async Task<(string QRUrl, string QRKey, string Message)> GetQRAsync()
    {
        try
        {
            // API from bilibili_learning_bot: passport.bilibili.com/x/passport-login/web/qrcode/generate
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://passport.bilibili.com/x/passport-login/web/qrcode/generate");
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            req.Headers.Add("Referer", "https://www.bilibili.com");

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return ("", "", $"HTTP {(int)resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Response: {code:0, data:{url:"https://passport.bilibili.com/h5-app/passport/login/scan?...", qrcode_key:"..."}}
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return ("", "", "响应格式错误");

            var url = data.GetProperty("url").GetString() ?? "";
            var key = data.GetProperty("qrcode_key").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(url))
                return ("", "", "未获取到二维码URL");

            Log($"Bili QR generated: key={key[..Math.Min(8, key.Length)]}");
            return (url, key, "请使用B站APP扫码");
        }
        catch (Exception ex)
        {
            Log($"Bili QR error: {ex.Message}");
            return ("", "", $"错误: {ex.Message}");
        }
    }

    /// <summary>Poll QR login status.</summary>
    public async Task<(bool Success, string Message)> PollAsync(string qrcodeKey)
    {
        try
        {
            // API from bilibili_learning_bot: passport.bilibili.com/x/passport-login/web/qrcode/poll
            var url = $"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrcodeKey}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0");
            req.Headers.Add("Referer", "https://www.bilibili.com");

            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var data = doc.RootElement.GetProperty("data");
            var code = data.GetProperty("code").GetInt32();

            return code switch
            {
                0 => (true, "登录成功"),
                86038 => (false, "二维码已过期"),
                86090 => (false, "已扫码，请在手机上确认"),
                86101 => (false, "等待扫码中..."),
                _ => (false, $"状态码: {code}")
            };
        }
        catch (Exception ex)
        {
            Log($"Bili poll error: {ex.Message}");
            return (false, $"轮询错误: {ex.Message}");
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BILI] {msg}\n"); } catch { }
    }
}
