using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Bilibili QR login service.
/// </summary>
public class BiliLoginService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public async Task<(string QRUrl, string Message)> GetQRAsync()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://passport.bilibili.com/x/passport-login/web/qrcode/generate");
            req.Headers.Add("User-Agent", "Mozilla/5.0");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return ("", $"HTTP {(int)resp.StatusCode}");

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");
            var url = data.GetProperty("url").GetString() ?? "";
            return (url, "请使用B站APP扫码");
        }
        catch (Exception ex)
        {
            return ("", $"错误: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> PollAsync(string qrcodeKey)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrcodeKey}");
            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var code = doc.RootElement.GetProperty("data").GetProperty("code").GetInt32();
            return code switch
            {
                0 => (true, "登录成功"),
                86038 => (false, "二维码已过期"),
                86090 => (false, "已扫码，请确认"),
                _ => (false, $"状态: {code}")
            };
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BILI] {msg}\n"); } catch { }
    }
}
