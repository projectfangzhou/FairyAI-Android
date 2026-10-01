using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Cloudflare relay config service.
/// </summary>
public class RelayConfigService
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    public (string Url, string Secret) LoadRelayConfig()
    {
        var config = ConfigManager.Load();
        return (config.Sync.SignalRUrl ?? "", "");
    }

    public void SaveRelayConfig(string url, string secret)
    {
        var config = ConfigManager.Load();
        config.Sync.SignalRUrl = url;
        ConfigManager.Save(config);
        if (!string.IsNullOrEmpty(secret))
        {
            var secretPath = Path.Combine(FileSystem.AppDataDirectory, "relay_secret.dat");
            File.WriteAllText(secretPath, secret);
        }
        Log($"Relay config saved: {url}");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [RELAY] {msg}\n"); } catch { }
    }
}
