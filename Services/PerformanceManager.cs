using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android performance manager - detect games/large apps and reduce resource usage.
/// </summary>
public class PerformanceManager
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    private static readonly HashSet<string> HeavyApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.tencent.tmgp.sgame", "com.tencent.tmgp.pubgmhd", "com.miHoYo.Yuanshen",
        "com.mojang.minecraftpe", "com.supercell.clashofclans", "com.epicgames.fortnite",
    };

    public bool IsHeavyAppRunning()
    {
        try
        {
            // Use ActivityManager to check running processes
            var am = Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService) as Android.App.ActivityManager;
            if (am != null)
            {
                var processes = am.RunningAppProcesses;
                if (processes != null)
                {
                    foreach (var p in processes)
                    {
                        var name = p.ProcessName ?? "";
                        if (HeavyApps.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)))
                        {
                            Log($"Heavy app detected: {name}");
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public HashSet<string> GetDisabledFeatures()
    {
        var config = ConfigManager.Load();
        var disabled = new HashSet<string>();
        if (config.Performance.Mode == "low")
        {
            disabled.Add("IdleSounds");
            disabled.Add("LocalVision");
        }
        else if (config.Performance.Mode == "balanced" && IsHeavyAppRunning())
        {
            disabled.Add("IdleSounds");
        }
        return disabled;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [PERF] {msg}\n"); } catch { }
    }
}

