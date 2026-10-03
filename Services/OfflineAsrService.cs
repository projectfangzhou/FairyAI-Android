using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Offline ASR — Whisper.cpp/ONNX local speech recognition.
/// No internet required for voice input.
/// </summary>
public class OfflineAsrService
{
    private readonly string _modelPath;
    private bool _isAvailable;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsAvailable => _isAvailable;

    public OfflineAsrService()
    {
        _modelPath = Path.Combine(FileSystem.AppDataDirectory, "whisper");
        _isAvailable = CheckModelAvailable();
    }

    /// <summary>Check if offline ASR model is available.</summary>
    private bool CheckModelAvailable()
    {
        try
        {
            var modelFile = Path.Combine(_modelPath, "ggml-base.bin");
            return File.Exists(modelFile);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Download Whisper model for offline use.</summary>
    public async Task<bool> DownloadModelAsync(string modelSize = "base")
    {
        try
        {
            Directory.CreateDirectory(_modelPath);
            var modelUrl = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{modelSize}.bin";
            var modelPath = Path.Combine(_modelPath, $"ggml-{modelSize}.bin");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var bytes = await http.GetByteArrayAsync(modelUrl);
            await File.WriteAllBytesAsync(modelPath, bytes);

            _isAvailable = true;
            Log($"Whisper model downloaded: {modelSize} ({bytes.Length / 1024 / 1024}MB)");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Model download error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Transcribe audio offline using Whisper.</summary>
    public async Task<string> TranscribeAsync(byte[] audioData)
    {
        if (!_isAvailable) return "离线ASR模型未安装";

        try
        {
            var audioPath = Path.Combine(_modelPath, "temp_input.wav");
            await File.WriteAllBytesAsync(audioPath, audioData);

            // Run whisper.cpp inference
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "whisper-cli",
                Arguments = $"-m \"{Path.Combine(_modelPath, "ggml-base.bin")}\" -f \"{audioPath}\" -l zh",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return "无法启动whisper";

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            File.Delete(audioPath);
            Log($"Transcribed: {output[..Math.Min(50, output.Length)]}");
            return output.Trim();
        }
        catch (Exception ex)
        {
            Log($"Transcribe error: {ex.Message}");
            return $"识别失败: {ex.Message}";
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [ASR] {msg}\n"); } catch { }
    }
}
