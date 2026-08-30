using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FairyAI_Android.Services;

public interface ITtsApiService
{
    Task<byte[]> SynthesizeAsync(string text, string? providerOverride = null, string? voiceOverride = null);
    bool IsAvailable { get; }
}

/// <summary>
/// API-based TTS service supporting MiMo, OpenAI, Azure, Google providers.
/// Uses OpenAI-compatible /v1/audio/speech endpoint or vendor-specific endpoints.
/// </summary>
public class TtsApiService : ITtsApiService
{
    private readonly HttpClient _http;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsAvailable
    {
        get
        {
            var config = ConfigManager.Load().TTS;
            return config.Provider != "System" && !string.IsNullOrWhiteSpace(config.ApiKey);
        }
    }

    public TtsApiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<byte[]> SynthesizeAsync(string text, string? providerOverride = null, string? voiceOverride = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

        var config = ConfigManager.Load().TTS;
        var provider = providerOverride ?? config.Provider;
        var voice = voiceOverride ?? config.Voice;

        Log($"TTS: provider={provider}, text='{text[..Math.Min(40, text.Length)]}...'");

        return provider switch
        {
            "MiMo TTS" or "MiMo TTS-VoiceClone" => await SynthesizeMiMoAsync(text, config, provider),
            "OpenAI TTS" => await SynthesizeOpenAIAsync(text, config, voice),
            "Azure TTS" => await SynthesizeAzureAsync(text, config, voice),
            "Google TTS" => await SynthesizeGoogleAsync(text, config, voice),
            _ => Array.Empty<byte>()
        };
    }

    private async Task<byte[]> SynthesizeMiMoAsync(string text, TTSConfig config, string provider)
    {
        try
        {
            var model = provider == "MiMo TTS-VoiceClone" ? "mimo-v2.5-tts-voiceclone" : "mimo-v2.5-tts";
            var voiceName = string.IsNullOrEmpty(config.Voice) ? "茉莉" : config.Voice;

            // MiMo TTS uses chat completions format with audio output
            var messages = new object[]
            {
                new { role = "user", content = "用温柔、轻柔、温暖的语气说话" },
                new { role = "assistant", content = text }
            };

            var payload = JsonSerializer.Serialize(new
            {
                model,
                messages,
                audio = new { format = "wav", voice = voiceName }
            });

            var url = string.IsNullOrEmpty(config.BaseUrl)
                ? "https://api.xiaomimimo.com/v1/chat/completions"
                : config.BaseUrl;

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("api-key", config.ApiKey);

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Log($"MiMo TTS error: {resp.StatusCode}");
                return Array.Empty<byte>();
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Extract audio.data from response
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("audio", out var audio) &&
                    audio.TryGetProperty("data", out var audioData))
                {
                    var audioStr = audioData.GetString();
                    if (!string.IsNullOrEmpty(audioStr))
                    {
                        var audioBytes = Convert.FromBase64String(audioStr);
                        Log($"MiMo TTS: {audioBytes.Length} bytes");
                        return audioBytes;
                    }
                }
            }

            Log("MiMo TTS: no audio in response");
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log($"MiMo TTS error: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private async Task<byte[]> SynthesizeOpenAIAsync(string text, TTSConfig config, string voice)
    {
        try
        {
            var url = string.IsNullOrEmpty(config.BaseUrl)
                ? "https://api.openai.com/v1/audio/speech"
                : config.BaseUrl;

            var model = string.IsNullOrEmpty(config.Model) ? "tts-1" : config.Model;
            var voiceName = string.IsNullOrEmpty(voice) ? "nova" : voice;

            var payload = JsonSerializer.Serialize(new
            {
                model,
                input = text,
                voice = voiceName,
                response_format = "mp3"
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Log($"OpenAI TTS error: {resp.StatusCode}");
                return Array.Empty<byte>();
            }

            var audioBytes = await resp.Content.ReadAsByteArrayAsync();
            Log($"OpenAI TTS: {audioBytes.Length} bytes");
            return audioBytes;
        }
        catch (Exception ex)
        {
            Log($"OpenAI TTS error: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private async Task<byte[]> SynthesizeAzureAsync(string text, TTSConfig config, string voice)
    {
        try
        {
            // Azure TTS REST API
            var url = string.IsNullOrEmpty(config.BaseUrl)
                ? $"https://eastus.tts.speech.microsoft.com/cognitiveservices/v1"
                : config.BaseUrl;

            var voiceName = string.IsNullOrEmpty(voice) ? "zh-CN-XiaoxiaoNeural" : voice;

            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'>
                <voice name='{voiceName}'>{SecurityEscapeXml(text)}</voice>
            </speak>";

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
            };
            req.Headers.Add("Ocp-Apim-Subscription-Key", config.ApiKey);
            req.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Log($"Azure TTS error: {resp.StatusCode}");
                return Array.Empty<byte>();
            }

            var audioBytes = await resp.Content.ReadAsByteArrayAsync();
            Log($"Azure TTS: {audioBytes.Length} bytes");
            return audioBytes;
        }
        catch (Exception ex)
        {
            Log($"Azure TTS error: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private async Task<byte[]> SynthesizeGoogleAsync(string text, TTSConfig config, string voice)
    {
        try
        {
            var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={config.ApiKey}";
            var voiceName = string.IsNullOrEmpty(voice) ? "zh-CN-Wavenet-A" : voice;

            var payload = JsonSerializer.Serialize(new
            {
                input = new { text },
                voice = new
                {
                    languageCode = "zh-CN",
                    name = voiceName,
                    ssmlGender = "NEUTRAL"
                },
                audioConfig = new
                {
                    audioEncoding = "MP3"
                }
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Log($"Google TTS error: {resp.StatusCode}");
                return Array.Empty<byte>();
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("audioContent", out var audioContent))
            {
                var audioStr = audioContent.GetString();
                if (!string.IsNullOrEmpty(audioStr))
                {
                    var audioBytes = Convert.FromBase64String(audioStr);
                    Log($"Google TTS: {audioBytes.Length} bytes");
                    return audioBytes;
                }
            }

            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log($"Google TTS error: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private static string SecurityEscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [TTS-API] {msg}\n"); } catch { }
    }
}
