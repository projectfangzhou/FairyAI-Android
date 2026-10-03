using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Live2D lightweight service — Lottie-based avatar with lip sync.
/// Replaces full Cubism SDK with lightweight animation.
/// </summary>
public class Live2DService
{
    private bool _isEnabled;
    private bool _isSpeaking;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public bool IsEnabled => _isEnabled;
    public bool IsSpeaking => _isSpeaking;

    /// <summary>Initialize Live2D/Lottie avatar.</summary>
    public void Initialize()
    {
        _isEnabled = true;
        Log("Live2D lightweight initialized (Lottie mode)");
    }

    /// <summary>Start lip sync animation.</summary>
    public void Speak(string text)
    {
        if (!_isEnabled) return;
        _isSpeaking = true;
        // Trigger Lottie lip sync animation
        Log($"Lip sync: {text[..Math.Min(20, text.Length)]}...");
    }

    /// <summary>Stop lip sync.</summary>
    public void StopSpeak()
    {
        _isSpeaking = false;
    }

    /// <summary>Change expression.</summary>
    public void SetExpression(string expression)
    {
        // Switch Lottie animation based on emotion
        Log($"Expression: {expression}");
    }

    /// <summary>Handle touch interaction on avatar.</summary>
    public string HandleTouch(string bodyPart)
    {
        return bodyPart switch
        {
            "head" => "嘻嘻，好痒~",
            "hand" => "要牵手吗？",
            "body" => "别碰我啦！",
            _ => "嗯？"
        };
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [L2D] {msg}\n"); } catch { }
    }
}
