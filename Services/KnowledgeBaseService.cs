using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android knowledge base - analyze PPT/Word/PDF files.
/// </summary>
public class KnowledgeBaseService
{
    private readonly string _kbFolder;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public KnowledgeBaseService()
    {
        _kbFolder = Path.Combine(FileSystem.AppDataDirectory, "knowledge");
        Directory.CreateDirectory(_kbFolder);
    }

    public string KbFolder => _kbFolder;

    public async Task<string> AnalyzeDocumentAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "文件不存在";
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string text = ext switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".docx" => await ExtractDocxTextAsync(filePath),
            ".pptx" => await ExtractPptxTextAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath)
        };

        if (string.IsNullOrWhiteSpace(text))
            return $"无法从 {ext} 文件提取文本";

        var kbFile = Path.Combine(_kbFolder, Path.GetFileNameWithoutExtension(filePath) + ".md");
        await File.WriteAllTextAsync(kbFile, $"# {Path.GetFileName(filePath)}\n\n{text}");
        Log($"Document analyzed: {Path.GetFileName(filePath)}");
        return text.Length > 2000 ? text[..2000] + "..." : text;
    }

    public List<string> ListKnowledge()
    {
        return Directory.GetFiles(_kbFolder, "*.md")
            .Select(f => Path.GetFileName(f)).OrderBy(f => f).ToList();
    }

    private static async Task<string> ExtractDocxTextAsync(string path)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return "";
            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();
            return System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim();
        }
        catch { return ""; }
    }

    private static async Task<string> ExtractPptxTextAsync(string path)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var sb = new System.Text.StringBuilder();
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide")))
            {
                using var reader = new StreamReader(entry.Open());
                var xml = await reader.ReadToEndAsync();
                sb.AppendLine(System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim());
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [KB] {msg}\n"); } catch { }
    }
}
