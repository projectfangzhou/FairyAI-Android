using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>GPT-SoVITS voice cloning TTS service.</summary>
public class GptSoVitsTtsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<byte[]> SynthesizeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();
        var config = ConfigManager.Load();
        var tts = config.TTS;
        if (string.IsNullOrWhiteSpace(tts.VoiceCloneAudioPath)) return Array.Empty<byte>();

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                text = text,
                text_lang = tts.VoiceCloneLang,
                ref_audio_path = tts.VoiceCloneAudioPath,
                prompt_text = tts.VoiceClonePromptText
            });
            var req = new HttpRequestMessage(HttpMethod.Post, "http://localhost:9880/tts")
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            };
            using var resp = await Http.SendAsync(req);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync() : Array.Empty<byte>();
        }
        catch { return Array.Empty<byte>(); }
    }
}
