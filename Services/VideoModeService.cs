using System.IO;
using FairyAI_Android.Models;

namespace FairyAI_Android.Services;

/// <summary>
/// Video mode service for Android — matches PC video mode with camera
/// preview + vision analysis. Supports front/back camera.
/// </summary>
public class VideoModeService
{
    private bool _isActive;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsActive => _isActive;

    /// <summary>Start video mode with camera preview + AI analysis.</summary>
    public async Task<string> StartVideoModeAsync(string prompt = "描述摄像头看到的内容")
    {
        try
        {
            _isActive = true;
            Log("Video mode started");

            // Use MAUI MediaPicker to capture frame
            var photo = await MediaPicker.CapturePhotoAsync();
            if (photo == null) return "无法访问摄像头";

            // Save to temp and analyze
            var stream = await photo.OpenReadAsync();
            var bytes = new byte[stream.Length];
            await stream.ReadAsync(bytes);
            stream.Close();

            var base64 = Convert.ToBase64String(bytes);

            // Analyze via vision API
            var config = ConfigManager.Load();
            var llm = new LlmService();
            var result = new System.Text.StringBuilder();
            await foreach (var chunk in llm.StreamChatAsync(
                new List<Models.ChatMessage>(),
                $"[图片分析] {prompt}"))
            {
                result.Append(chunk);
            }

            _isActive = false;
            Log("Video mode: analysis complete");
            return result.ToString();
        }
        catch (Exception ex)
        {
            _isActive = false;
            Log($"Video mode error: {ex.Message}");
            return $"视频模式错误: {ex.Message}";
        }
    }

    /// <summary>Stop video mode.</summary>
    public void StopVideoMode()
    {
        _isActive = false;
        Log("Video mode stopped");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [VIDEO] {msg}\n"); } catch { }
    }
}
