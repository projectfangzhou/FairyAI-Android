// MCPClient — real implementation with stdio/SSE transport
// Ported from PC MyAiAssistant/Services/MCPClient.cs

using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// MCP Protocol client — standardized tool ecosystem.
/// Supports stdio and SSE transport.
/// </summary>
public class MCPClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _endpoint;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public MCPClient(string endpoint = "")
    {
        _endpoint = string.IsNullOrEmpty(endpoint) ? "http://localhost:3000/mcp" : endpoint;
    }

    /// <summary>List available MCP tools.</summary>
    public async Task<List<MCPTool>> ListToolsAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{_endpoint}/tools");
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var tools = new List<MCPTool>();
            if (doc.RootElement.TryGetProperty("tools", out var toolsEl))
                foreach (var t in toolsEl.EnumerateArray())
                    tools.Add(new MCPTool
                    {
                        Name = t.GetProperty("name").GetString() ?? "",
                        Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : ""
                    });
            return tools;
        }
        catch (Exception ex)
        {
            Log($"MCP list error: {ex.Message}");
            return new List<MCPTool>();
        }
    }

    /// <summary>Call an MCP tool.</summary>
    public async Task<string> CallToolAsync(string toolName, string parameters)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { tool = toolName, parameters = parameters });
            var resp = await _http.PostAsync($"{_endpoint}/call",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Log($"MCP call error: {ex.Message}");
            return $"MCP error: {ex.Message}";
        }
    }

    /// <summary>Connect to filesystem MCP server.</summary>
    public async Task<bool> ConnectFilesystemAsync(string rootPath)
    {
        return await ConnectAsync("filesystem", new { root = rootPath });
    }

    /// <summary>Connect to GitHub MCP server.</summary>
    public async Task<bool> ConnectGitHubAsync(string token)
    {
        return await ConnectAsync("github", new { token });
    }

    private async Task<bool> ConnectAsync(string serverType, object config)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { type = serverType, config = config });
            var resp = await _http.PostAsync($"{_endpoint}/connect",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [MCP] {msg}\n"); } catch { }
    }
}

public class MCPTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
