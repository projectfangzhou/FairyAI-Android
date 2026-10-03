using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>OpenRouter unified provider service.</summary>
public class OpenRouterService
{
    public static readonly string ChatEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public static readonly string SpeechEndpoint = "https://openrouter.ai/api/v1/audio/speech";

    public static Dictionary<string, string> GetHeaders(string apiKey)
    {
        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}",
            ["X-Title"] = "FairyAI"
        };
    }
}

/// <summary>Local model management (llama.cpp/ONNX).</summary>
public class LocalModelService
{
    private readonly string _modelsDir;

    public LocalModelService()
    {
        _modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(_modelsDir);
    }

    public List<LocalModel> ListModels()
    {
        var models = new List<LocalModel>();
        foreach (var file in Directory.GetFiles(_modelsDir, "*.gguf"))
        {
            var info = new FileInfo(file);
            models.Add(new LocalModel { Name = Path.GetFileNameWithoutExtension(file), Path = file, SizeBytes = info.Length, Format = "gguf" });
        }
        return models;
    }
}

public class LocalModel
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Format { get; set; } = "";
}
