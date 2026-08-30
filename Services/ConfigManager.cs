using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FairyAI_Android.Services;

public class AppConfig
{
    public LLMConfig LLM { get; set; } = new();
    public LLMConfig FallbackLLM { get; set; } = new();
    public ASRConfig ASR { get; set; } = new();
    public TTSConfig TTS { get; set; } = new();
    public VisionConfig Vision { get; set; } = new();
    public SyncConfig Sync { get; set; } = new();
    public PersonalityConfig Personality { get; set; } = new();
    public WakeWordConfig WakeWord { get; set; } = new();
}

public class LLMConfig
{
    public string Provider { get; set; } = "Kimi";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.moonshot.cn/v1/chat/completions";
    public string Model { get; set; } = "kimi-k2.6";
}

public class ASRConfig
{
    public string Provider { get; set; } = "System";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
}

public class TTSConfig
{
    public string Provider { get; set; } = "System";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string Voice { get; set; } = "";
}

public class VisionConfig
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
}

public class SyncConfig
{
    public bool Enabled { get; set; } = false;
    public string DeviceName { get; set; } = "FairyAI-Android";
    public bool EnableBluetooth { get; set; } = true;
    public bool EnableLan { get; set; } = true;
    public string SignalRUrl { get; set; } = "";
    public int LanPort { get; set; } = 9876;
    public string PairingCode { get; set; } = "";
}

/// <summary>
/// AI personality configuration. The system prompt defines the AI's character, tone, and behavior.
/// </summary>
public class PersonalityConfig
{
    public string Name { get; set; } = "Fairy";
    public string SystemPrompt { get; set; } = "你是 Fairy，一个温柔、友善的AI助手。你说话亲切自然，像朋友一样和用户交流。你喜欢用轻松的语气回答问题，偶尔会关心用户的状态。";
    public string AvatarUrl { get; set; } = "";
    public string Language { get; set; } = "zh-CN";
    public bool EnableEmotion { get; set; } = true;
}

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        FileSystem.AppDataDirectory, "config.json");

    public static readonly Dictionary<string, (string BaseUrl, string Model)> LLMProviders = new()
    {
        ["Kimi"] = ("https://api.moonshot.cn/v1/chat/completions", "kimi-k2.6"),
        ["DeepSeek"] = ("https://api.deepseek.com/v1/chat/completions", "deepseek-chat"),
        ["MiMo"] = ("https://api.xiaomimimo.com/v1/chat/completions", "mimo-v2.5"),
        ["OpenAI"] = ("https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
    };

    public static readonly Dictionary<string, (string BaseUrl, string Model, string[] Voices)> TTSProviders = new()
    {
        ["System"] = ("", "", Array.Empty<string>()),
        ["MiMo TTS"] = ("https://api.xiaomimimo.com/v1/chat/completions", "mimo-v2.5-tts",
            new[] { "茉莉", "冰糖", "苏打", "白桦" }),
        ["MiMo TTS-VoiceClone"] = ("https://api.xiaomimimo.com/v1/chat/completions", "mimo-v2.5-tts-voiceclone",
            new[] { "自定义" }),
        ["OpenAI TTS"] = ("https://api.openai.com/v1/audio/speech", "tts-1",
            new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" }),
        ["Azure TTS"] = ("", "zh-CN-XiaoxiaoNeural",
            new[] { "zh-CN-XiaoxiaoNeural", "zh-CN-YunxiNeural", "zh-CN-XiaoyiNeural", "en-US-JennyNeural" }),
        ["Google TTS"] = ("https://texttospeech.googleapis.com/v1/text:synthesize", "",
            new[] { "zh-CN-Wavenet-A", "zh-CN-Wavenet-B", "en-US-Wavenet-A" }),
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static bool NeedsSetup()
    {
        var config = Load();
        return string.IsNullOrWhiteSpace(config.LLM.ApiKey);
    }

    /// <summary>
    /// Generate a 6-digit pairing code for device verification.
    /// </summary>
    public static string GeneratePairingCode()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var code = BitConverter.ToUInt32(bytes) % 1000000;
        return code.ToString("D6");
    }

    /// <summary>
    /// Hash a pairing code for secure storage (never store plaintext).
    /// </summary>
    public static string HashPairingCode(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code + "FairyAI_Salt_2024");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Verify a pairing code against a stored hash.
    /// </summary>
    public static bool VerifyPairingCode(string code, string storedHash)
    {
        return HashPairingCode(code) == storedHash;
    }

    /// <summary>
    /// Export config for sync (excludes sensitive local-only fields).
    /// </summary>
    public static string ExportForSync(AppConfig config)
    {
        var syncData = new
        {
            personality = config.Personality,
            llm = new { config.LLM.Provider, config.LLM.BaseUrl, config.LLM.Model },
            tts = new { config.TTS.Provider, config.TTS.BaseUrl, config.TTS.Model, config.TTS.Voice },
            sync = new { config.Sync.DeviceName }
        };
        return JsonSerializer.Serialize(syncData, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Apply synced config from PC (merges personality and API keys).
    /// </summary>
    public static AppConfig ApplySyncedConfig(AppConfig current, string syncedJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(syncedJson);
            var root = doc.RootElement;

            // Sync personality
            if (root.TryGetProperty("personality", out var personality))
            {
                if (personality.TryGetProperty("name", out var name))
                    current.Personality.Name = name.GetString() ?? current.Personality.Name;
                if (personality.TryGetProperty("systemPrompt", out var prompt))
                    current.Personality.SystemPrompt = prompt.GetString() ?? current.Personality.SystemPrompt;
                if (personality.TryGetProperty("language", out var lang))
                    current.Personality.Language = lang.GetString() ?? current.Personality.Language;
                if (personality.TryGetProperty("enableEmotion", out var emotion))
                    current.Personality.EnableEmotion = emotion.GetBoolean();
            }

            // Sync LLM config (keep API key from current device)
            if (root.TryGetProperty("llm", out var llm))
            {
                if (llm.TryGetProperty("provider", out var p)) current.LLM.Provider = p.GetString() ?? current.LLM.Provider;
                if (llm.TryGetProperty("baseUrl", out var url)) current.LLM.BaseUrl = url.GetString() ?? current.LLM.BaseUrl;
                if (llm.TryGetProperty("model", out var m)) current.LLM.Model = m.GetString() ?? current.LLM.Model;
            }

            // Sync TTS config
            if (root.TryGetProperty("tts", out var tts))
            {
                if (tts.TryGetProperty("provider", out var p)) current.TTS.Provider = p.GetString() ?? current.TTS.Provider;
                if (tts.TryGetProperty("baseUrl", out var url)) current.TTS.BaseUrl = url.GetString() ?? current.TTS.BaseUrl;
                if (tts.TryGetProperty("model", out var m)) current.TTS.Model = m.GetString() ?? current.TTS.Model;
                if (tts.TryGetProperty("voice", out var v)) current.TTS.Voice = v.GetString() ?? current.TTS.Voice;
            }
        }
        catch { }

        return current;
    }
}
