using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>MCP Protocol client for standardized tool ecosystem.</summary>
public class MCPClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _endpoint;

    public MCPClient(string endpoint = "")
    {
        _endpoint = string.IsNullOrEmpty(endpoint) ? "http://localhost:3000/mcp" : endpoint;
    }

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
                    tools.Add(new MCPTool { Name = t.GetProperty("name").GetString() ?? "" });
            return tools;
        }
        catch { return new List<MCPTool>(); }
    }

    public async Task<string> CallToolAsync(string toolName, string parameters)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { tool = toolName, parameters = parameters });
            var resp = await _http.PostAsync($"{_endpoint}/call",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return $"MCP error: {ex.Message}"; }
    }
}

public class MCPTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
