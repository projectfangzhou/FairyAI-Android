using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android screen automation service using Accessibility API.
/// </summary>
public class ScreenAutomationService
{
    private readonly AccessibilityService _a11y;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public ScreenAutomationService()
    {
        _a11y = new AccessibilityService();
    }

    public bool IsAvailable => _a11y.IsEnabled;

    public bool Tap(int x, int y, string currentPackage = "")
    {
        if (!string.IsNullOrEmpty(currentPackage) && AccessibilityService.IsPackageBlocked(currentPackage))
        {
            Log($"BLOCKED: tap on {currentPackage}");
            return false;
        }
        return _a11y.Tap(x, y, currentPackage);
    }

    public bool Swipe(int x1, int y1, int x2, int y2, string currentPackage = "")
    {
        if (!string.IsNullOrEmpty(currentPackage) && AccessibilityService.IsPackageBlocked(currentPackage))
            return false;
        return _a11y.Swipe(x1, y1, x2, y2, currentPackage);
    }

    public bool TypeText(string text, string currentPackage = "")
    {
        if (!string.IsNullOrEmpty(currentPackage) && AccessibilityService.IsPackageBlocked(currentPackage))
            return false;
        return _a11y.TypeText(text, currentPackage);
    }

    public string GetScreenDescription() => _a11y.GetScreenDescription();

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SCREEN] {msg}\n"); } catch { }
    }
}
