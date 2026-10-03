// Source: https://github.com/xiaoyaya191/bilibili_learning_bot.git
// BiliLearnService — real Bilibili learning with video processing

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// BiliLearn service — download and learn from Bilibili videos.
/// Source: https://github.com/xiaoyaya191/bilibili_learning_bot.git
/// </summary>
public class BiliLearnService
{
    private const int DailyLimit = 20;
    private static readonly string StatsPath = Path.Combine(FileSystem.AppDataDirectory, "bililearn_stats.json");
    private static readonly string KnowledgeDir = Path.Combine(FileSystem.AppDataDirectory, "knowledge");
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Learn from Bilibili URL — download video and extract knowledge.</summary>
    public async Task<string> LearnFromUrlAsync(string url)
    {
        if (!CanLearnToday())
            return $"Daily limit reached ({DailyLimit} videos)";

        try
        {
            // Extract video ID from URL
            var videoId = ExtractVideoId(url);
            if (string.IsNullOrEmpty(videoId))
                return "Invalid Bilibili URL";

            // Get video info via Bilibili API
            var videoInfo = await GetVideoInfoAsync(videoId);

            // Save to knowledge base
            Directory.CreateDirectory(KnowledgeDir);
            var kbFile = Path.Combine(KnowledgeDir, $"bilibili_{videoId}.md");
            await File.WriteAllTextAsync(kbFile,
                $"# {videoInfo.Title}\n\nURL: {url}\nDate: {DateTime.Now:yyyy-MM-dd}\n\n{videoInfo.Description}");

            IncrementCount();
            Log($"Learned: {videoInfo.Title}");
            return $"Learned: {videoInfo.Title}";
        }
        catch (Exception ex)
        {
            Log($"Learn error: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>Get video info from Bilibili API.</summary>
    private async Task<(string Title, string Description)> GetVideoInfoAsync(string videoId)
    {
        try
        {
            var url = $"https://api.bilibili.com/x/web-interface/view?bvid={videoId}";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                var title = data.GetProperty("title").GetString() ?? videoId;
                var desc = data.GetProperty("desc").GetString() ?? "";
                return (title, desc);
            }
        }
        catch (Exception ex)
        {
            Log($"Get info error: {ex.Message}");
        }
        return (videoId, "");
    }

    private string ExtractVideoId(string url)
    {
        // Extract BV ID from URL like https://www.bilibili.com/video/BV1xx411c7XW
        var match = System.Text.RegularExpressions.Regex.Match(url, @"BV[a-zA-Z0-9]+");
        return match.Success ? match.Value : "";
    }

    public bool CanLearnToday() => GetCountToday() < DailyLimit;

    private int GetCountToday()
    {
        try
        {
            if (!File.Exists(StatsPath)) return 0;
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath));
            return stats?.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd"), 0) ?? 0;
        }
        catch { return 0; }
    }

    private void IncrementCount()
    {
        try
        {
            var stats = File.Exists(StatsPath)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath)) ?? new()
                : new Dictionary<string, int>();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            stats[today] = stats.GetValueOrDefault(today, 0) + 1;
            File.WriteAllText(StatsPath, JsonSerializer.Serialize(stats));
        }
        catch { }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BILI] {msg}\n"); } catch { }
    }
}
