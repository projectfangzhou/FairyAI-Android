using System.Text.Json;
using System.Text.Json.Nodes;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Tool Registry 鈥?manages AI tools for Function Calling.
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    public void Register(ITool tool) => _tools[tool.Name] = tool;
    public ITool? GetTool(string name) => _tools.TryGetValue(name, out var t) ? t : null;
    public IReadOnlyCollection<ITool> GetAll() => _tools.Values;
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    List<ToolParameter> Parameters { get; }
    Task<string> ExecuteAsync(JsonObject args);
}

public class ToolParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public string Description { get; set; } = "";
    public bool Required { get; set; } = true;
}

/// <summary>
/// Android Function Calling Service 鈥?12-round tool loop.
/// </summary>
public class FunctionCallingService
{
    private readonly ILlmService _llm;
    private readonly ToolRegistry _tools;
    private const int MaxToolRounds = 12;

    public FunctionCallingService(ILlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
    }

    public async Task<string> ChatWithToolsAsync(string systemPrompt, string userMessage)
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userMessage }
        };

        for (int round = 0; round < MaxToolRounds; round++)
        {
            var (content, toolCalls) = await CallLlmWithToolsAsync(messages);
            if (toolCalls == null || toolCalls.Count == 0)
                return content;

            messages.Add(new ChatMessage { Role = "assistant", Content = content });
            foreach (var tc in toolCalls)
            {
                var tool = _tools.GetTool(tc.Name);
                if (tool != null)
                {
                    var result = await tool.ExecuteAsync(
                        JsonSerializer.Deserialize<JsonObject>(tc.Arguments) ?? new JsonObject());
                    messages.Add(new ChatMessage { Role = "tool", Content = $"[{tc.Name}] {result}" });
                }
            }
        }
        return "Task complete.";
    }

    private async Task<(string Content, List<ToolCall>? ToolCalls)> CallLlmWithToolsAsync(List<ChatMessage> messages)
    {
        var config = ConfigManager.Load();
        // Build tools definition for OpenAI-compatible API
        var toolsDef = _tools.GetAll().Select(t => new {
            type = "function",
            function = new {
                name = t.Name,
                description = t.Description,
                parameters = new { type = "object", properties = new { } }
            }
        }).ToArray();

        // Use non-streaming HTTP request to get complete JSON with tool_calls
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new {
                model = config.LLM.Model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                max_tokens = 1000,
                tools = toolsDef
            });

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, config.LLM.BaseUrl)
            {
                Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(config.LLM.ApiKey))
                req.Headers.Add("Authorization", $"Bearer {config.LLM.ApiKey}");

            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(body);
            var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            var content = msg.GetProperty("content").GetString() ?? "";

            List<ToolCall>? toolCalls = null;
            if (msg.TryGetProperty("tool_calls", out var calls) && calls.GetArrayLength() > 0)
            {
                toolCalls = new List<ToolCall>();
                foreach (var call in calls.EnumerateArray())
                {
                    toolCalls.Add(new ToolCall {
                        Name = call.GetProperty("function").GetProperty("name").GetString() ?? "",
                        Arguments = call.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
                    });
                }
            }

            return (content, toolCalls);
        }
        catch (Exception ex)
        {
            return ($"LLM error: {ex.Message}", null);
        }
    }

    private List<ToolCall>? ParseToolCalls(string response)
    {
        // Try to parse tool_calls from LLM response
        try
        {
            var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tool_calls", out var calls))
            {
                var list = new List<ToolCall>();
                foreach (var call in calls.EnumerateArray())
                {
                    list.Add(new ToolCall {
                        Name = call.GetProperty("function").GetProperty("name").GetString() ?? "",
                        Arguments = call.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
                    });
                }
                return list.Count > 0 ? list : null;
            }
        }
        catch { }
        return null;
    }
}

public class ToolCall
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "{}";
}

/// <summary>
/// Android Capability Registry 鈥?declares all AI capabilities.
/// </summary>
public class CapabilityRegistry
{
    public static string BuildSystemPrompt(AppConfig config)
    {
        return @"浣犳槸 FairyAI锛屼竴涓姛鑳藉己澶х殑涓汉AI鍔╂墜銆備綘鎷ユ湁浠ヤ笅鑳藉姏锛屾牴鎹敤鎴烽渶姹傝嚜涓婚€夋嫨宸ュ叿锛?
## 鏍稿績鑳藉姏
1. 灞忓箷鑷姩鍖? 鎴睆銆佺偣鍑汇€佽緭鍏ャ€佹寜閿?2. 璇煶鍚堟垚: TTS璇煶鍏嬮殕
3. 鐭ヨ瘑搴? 鏂囨。鍒嗘瀽銆佺瑪璁扮鐞?4. 鑱旂綉鎼滅储: Web鎼滅储
5. 绯荤粺宸ュ叿: 鏃堕棿銆佽绠椼€佸簲鐢ㄦ墦寮€
6. 缈昏瘧: 澶氳瑷€缈昏瘧
7. 鎻愰啋: 鏃ョ▼绠＄悊

## 浜烘牸璁惧畾
" + config.Personality.SystemPrompt;
    }

    public static string GetCapabilitiesJson(ToolRegistry tools)
    {
        return JsonSerializer.Serialize(new
        {
            name = "FairyAI",
            version = "2.0.0",
            tools = tools.GetAll().Select(t => new { t.Name, t.Description })
        });
    }
}



