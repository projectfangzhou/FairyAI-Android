// MarkdownService — real markdown to HTML conversion
// Android implementation

using System.Text.RegularExpressions;

namespace FairyAI_Android.Services;

/// <summary>Markdown rendering service.</summary>
public class MarkdownService
{
    public string RenderHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";

        var html = markdown;

        // Headers
        html = Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

        // Bold and italic
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<b>$1</b>");
        html = Regex.Replace(html, @"\*(.+?)\*", "<i>$1</i>");

        // Code blocks
        html = Regex.Replace(html, @"```(\w*)\n(.*?)```", "<pre><code>$2</code></pre>", RegexOptions.Singleline);

        // Inline code
        html = Regex.Replace(html, @"`(.+?)`", "<code>$1</code>");

        // Links
        html = Regex.Replace(html, @"\[(.+?)\]\((.+?)\)", "<a href=\"$2\">$1</a>");

        // Line breaks
        html = html.Replace("\n\n", "<br/><br/>");
        html = html.Replace("\n", "<br/>");

        return html;
    }
}
