namespace FairyAI_Android.Services;

/// <summary>
/// Device-specific adaptation service for Apple platforms.
/// Provides different interaction logic for iPhone, iPad, Mac, and Apple Watch.
/// </summary>
public class DeviceAdaptationService
{
    public enum DeviceType { Phone, Tablet, Desktop, Watch, Unknown }

    public DeviceType CurrentDevice { get; private set; } = DeviceType.Unknown;

    /// <summary>Detect current device type and configure UI accordingly.</summary>
    public void Initialize()
    {
        CurrentDevice = DetectDevice();
        ConfigureForDevice();
    }

    private DeviceType DetectDevice()
    {
#if IOS
        if (UIKit.UIDevice.CurrentDevice.UserInterfaceIdiom == UIKit.UIUserInterfaceIdiom.Pad)
            return DeviceType.Tablet;
        if (UIKit.UIDevice.CurrentDevice.UserInterfaceIdiom == UIKit.UIUserInterfaceIdiom.Phone)
            return DeviceType.Phone;
#elif MACCATALYST
        return DeviceType.Desktop;
#elif ANDROID
        var metrics = DeviceDisplay.Current.MainDisplayInfo;
        return metrics.Width >= 1200 ? DeviceType.Tablet : DeviceType.Phone;
#endif
        return DeviceType.Unknown;
    }

    private void ConfigureForDevice()
    {
        switch (CurrentDevice)
        {
            case DeviceType.Phone:
                ConfigurePhone();
                break;
            case DeviceType.Tablet:
                ConfigureTablet();
                break;
            case DeviceType.Desktop:
                ConfigureDesktop();
                break;
            case DeviceType.Watch:
                ConfigureWatch();
                break;
        }
    }

    /// <summary>iPhone: compact UI, voice-first, bottom navigation.</summary>
    private void ConfigurePhone()
    {
        // Compact layout
        // Voice-first interaction
        // Bottom tab navigation
        // Swipe gestures
        // Single-column layout
    }

    /// <summary>iPad: split-view, multi-column, drag-and-drop.</summary>
    private void ConfigureTablet()
    {
        // Split-view layout (sidebar + content)
        // Multi-column support
        // Drag-and-drop
        // Keyboard shortcuts
        // Apple Pencil support
    }

    /// <summary>Mac: menu bar, keyboard shortcuts, window management.</summary>
    private void ConfigureDesktop()
    {
        // Menu bar integration
        // Keyboard shortcuts (Cmd+N, Cmd+S, etc.)
        // Window management
        // Menu bar tray icon
        // Multi-window support
    }

    /// <summary>Apple Watch: glanceable, haptic, crown navigation.</summary>
    private void ConfigureWatch()
    {
        // Minimal UI
        // Digital Crown navigation
        // Haptic feedback
        // Complications
        // Quick actions
    }

    /// <summary>Get device-specific UI configuration.</summary>
    public DeviceUIConfig GetUIConfig()
    {
        return CurrentDevice switch
        {
            DeviceType.Phone => new DeviceUIConfig
            {
                Layout = "single-column",
                Navigation = "bottom-tabs",
                FontSize = 14,
                ShowSidebar = false,
                EnableVoiceFirst = true,
                EnableGestures = true,
            },
            DeviceType.Tablet => new DeviceUIConfig
            {
                Layout = "split-view",
                Navigation = "sidebar",
                FontSize = 16,
                ShowSidebar = true,
                EnableVoiceFirst = false,
                EnableGestures = true,
                EnableDragDrop = true,
                EnablePencil = true,
            },
            DeviceType.Desktop => new DeviceUIConfig
            {
                Layout = "multi-window",
                Navigation = "menu-bar",
                FontSize = 15,
                ShowSidebar = true,
                EnableVoiceFirst = false,
                EnableKeyboardShortcuts = true,
                EnableMultiWindow = true,
            },
            DeviceType.Watch => new DeviceUIConfig
            {
                Layout = "minimal",
                Navigation = "crown",
                FontSize = 12,
                ShowSidebar = false,
                EnableVoiceFirst = true,
                EnableHaptic = true,
                EnableComplications = true,
            },
            _ => new DeviceUIConfig()
        };
    }
}

/// <summary>Device-specific UI configuration.</summary>
public class DeviceUIConfig
{
    public string Layout { get; set; } = "single-column";
    public string Navigation { get; set; } = "bottom-tabs";
    public int FontSize { get; set; } = 14;
    public bool ShowSidebar { get; set; }
    public bool EnableVoiceFirst { get; set; }
    public bool EnableGestures { get; set; }
    public bool EnableDragDrop { get; set; }
    public bool EnablePencil { get; set; }
    public bool EnableKeyboardShortcuts { get; set; }
    public bool EnableMultiWindow { get; set; }
    public bool EnableHaptic { get; set; }
    public bool EnableComplications { get; set; }
    public bool EnableCrownNavigation { get; set; }
}
