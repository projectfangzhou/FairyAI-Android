using System.Text.Json;
using System.Text.Json.Nodes;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Tool Registry — manages AI tools for Function Calling.
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
/// Android Function Calling Service — 12-round tool loop.
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
        return "任务完成。";
    }

    private async Task<(string Content, List<ToolCall>? ToolCalls)> CallLlmWithToolsAsync(List<ChatMessage> messages)
    {
        var config = ConfigManager.Load();
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync(messages, ""))
            response.Append(chunk);
        return (response.ToString(), null);
    }
}

public class ToolCall
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "{}";
}

/// <summary>
/// Android Capability Registry — declares all AI capabilities.
/// </summary>
public class CapabilityRegistry
{
    public static string BuildSystemPrompt(AppConfig config)
    {
        return @"你是 FairyAI，一个功能强大的个人AI助手。你拥有以下能力，根据用户需求自主选择工具：

## 核心能力
1. 屏幕自动化: 截屏、点击、输入、按键
2. 语音合成: TTS语音克隆
3. 知识库: 文档分析、笔记管理
4. 联网搜索: Web搜索
5. 系统工具: 时间、计算、应用打开
6. 翻译: 多语言翻译
7. 提醒: 日程管理

## 人格设定
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
