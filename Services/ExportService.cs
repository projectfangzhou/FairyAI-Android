// ExportService — real conversation export with file save
// Android implementation

using System.IO;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>Export service for conversations.</summary>
public class ExportService
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>Export conversation to Markdown file.</summary>
    public async Task<string> ExportToMarkdownAsync(List<ChatMessage> messages, string title = "conversation")
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            foreach (var m in messages)
            {
                sb.AppendLine($"## {m.Role}");
                sb.AppendLine(m.Content);
                sb.AppendLine();
            }

            var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            var filePath = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString());
            Log($"Exported to: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Log($"Export error: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>Export conversation to plain text.</summary>
    public async Task<string> ExportToTextAsync(List<ChatMessage> messages, string title = "conversation")
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in messages)
                sb.AppendLine($"{m.Role}: {m.Content}");

            var fileName = $"{title}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var filePath = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString());
            return filePath;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [EXPORT] {msg}\n"); } catch { }
    }
}
