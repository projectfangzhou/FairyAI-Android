using System.IO;
using System.Text.Json;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>Agent service — task planning + execution loop + long-term memory.</summary>
public class AgentService
{
    private readonly ILlmService _llm;
    private readonly ToolRegistry _tools;
    private readonly VectorDatabase _memory;

    public AgentService(ILlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
        _memory = new VectorDatabase(Path.Combine(FileSystem.AppDataDirectory, "agent_memory.json"));
    }

    public async Task<string> ExecuteTaskAsync(string task, int maxSteps = 10)
    {
        var results = new List<string>();
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = "你是任务执行Agent。将任务分解为步骤并执行。" },
            new() { Role = "user", Content = task }
        };

        for (int i = 0; i < maxSteps; i++)
        {
            var response = new System.Text.StringBuilder();
            await foreach (var chunk in _llm.StreamChatAsync(messages, ""))
                response.Append(chunk);

            var result = response.ToString();
            results.Add(result);

            // Store in memory
            var id = Guid.NewGuid().ToString("N")[..8];
            _memory.Add(id, new float[256], result);
            _memory.Save();

            if (result.Contains("TASK_COMPLETE")) break;
        }

        return string.Join("\n", results);
    }
}
