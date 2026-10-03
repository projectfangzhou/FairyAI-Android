// Source: bilibili_learning_bot
// Android Web Search Service — Chromium/WebView-based search

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android web search using Chromium/WebView engine.
/// </summary>
public class ChromiumWebSearchService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Search web using Chromium engine.</summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5)
    {
        try
        {
            // DuckDuckGo HTML search (works on Android without WebView)
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36");

            using var resp = await Http.SendAsync(req);
            var html = await resp.Content.ReadAsStringAsync();

            var results = new List<SearchResult>();
            // Parse HTML for results
            var lines = html.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("result__a") && line.Contains("href"))
                {
                    var title = System.Text.RegularExpressions.Regex.Match(line, ">([^<]+)</a>").Groups[1].Value;
                    var link = System.Text.RegularExpressions.Regex.Match(line, "href=\"([^\"]+)\"").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(title) && results.Count < maxResults)
                    {
                        results.Add(new SearchResult { Title = title.Trim(), Url = link, Snippet = "" });
                    }
                }
            }

            Log($"Search '{query}': {results.Count} results");
            return results;
        }
        catch (Exception ex)
        {
            Log($"Search error: {ex.Message}");
            return new List<SearchResult>();
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SEARCH] {msg}\n"); } catch { }
    }
}

public class SearchResult
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Snippet { get; set; } = "";
}
