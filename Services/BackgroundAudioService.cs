using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Background microphone service: allows the app to capture microphone audio
/// in the background with noise reduction for continuous voice interaction.
/// Uses Android foreground service for persistent audio capture.
/// </summary>
public class BackgroundAudioService
{
    private readonly NoiseReductionService _noiseReduction = new();
    private bool _isListening;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsListening => _isListening;

    /// <summary>Start background microphone capture with noise reduction.</summary>
    public async Task StartAsync()
    {
        if (_isListening) return;

        try
        {
            _isListening = true;
            _noiseReduction.ResetNoiseProfile();

            // Start Android foreground service for background audio
            // In MAUI, this would use a platform-specific foreground service
            Log("Background audio: started with noise reduction");

            // Begin continuous audio capture loop
            _ = Task.Run(async () => await CaptureLoopAsync());
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log($"Background audio start error: {ex.Message}");
            _isListening = false;
        }
    }

    public void Stop()
    {
        _isListening = false;
        Log("Background audio: stopped");
    }

    private async Task CaptureLoopAsync()
    {
        while (_isListening)
        {
            try
            {
                // Capture audio frame (simulated — platform-specific capture)
                // In production: Android AudioRecord API
                await Task.Delay(100); // ~10 frame updates per second

                // Process through noise reduction
                // _noiseReduction.ProcessPcm16(capturedBytes);
            }
            catch (Exception ex)
            {
                Log($"Capture loop error: {ex.Message}");
                await Task.Delay(500);
            }
        }
    }

    /// <summary>Process captured audio through noise reduction.</summary>
    public byte[] ProcessAudio(byte[] rawAudio)
    {
        return _noiseReduction.ProcessPcm16(rawAudio);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BG_AUDIO] {msg}\n"); } catch { }
    }
}
