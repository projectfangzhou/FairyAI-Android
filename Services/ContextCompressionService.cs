using System.IO;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>
/// Android context compression service.
/// </summary>
public class ContextCompressionService
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool ShouldCompress(IReadOnlyList<ChatMessage> history)
    {
        var config = ConfigManager.Load();
        if (!config.Context.Enabled) return false;
        long totalChars = history.Sum(m => m.Content?.Length ?? 0);
        return totalChars / 2 >= config.Context.AutoCompressTokens;
    }

    public async Task<List<ChatMessage>> CompressAsync(List<ChatMessage> history)
    {
        if (history.Count < 4) return history;
        var toSummarize = history.Take(history.Count - 6).ToList();
        var recent = history.TakeLast(6).ToList();
        var conversationText = string.Join("\n", toSummarize.Select(m => $"{m.Role}: {m.Content}"));
        var summaryPrompt = "请将以下对话压缩为简要摘要，保留关键信息。\n\n" + conversationText;

        try
        {
            var llm = new LlmService();
            var summary = new System.Text.StringBuilder();
            await foreach (var chunk in llm.StreamChatAsync(new List<ChatMessage>(), summaryPrompt))
                summary.Append(chunk);

            var result = new List<ChatMessage>
            {
                new() { Role = "system", Content = $"[对话摘要] {summary}" }
            };
            result.AddRange(recent);
            return result;
        }
        catch { return history; }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [CTX] {msg}\n"); } catch { }
    }
}
