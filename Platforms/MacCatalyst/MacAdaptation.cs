using AppKit;
using Foundation;

namespace FairyAI_Android.Platforms.MacCatalyst;

/// <summary>
/// macOS-specific adaptations for Mac Catalyst.
/// </summary>
public static class MacAdaptation
{
    /// <summary>Configure macOS-specific UI and behavior.</summary>
    public static void Configure()
    {
        // Configure menu bar
        ConfigureMenuBar();

        // Configure window management
        ConfigureWindowManagement();

        // Configure keyboard shortcuts
        ConfigureKeyboardShortcuts();
    }

    private static void ConfigureMenuBar()
    {
        // Add menu bar items
        var menuBar = NSApplication.SharedApplication.MainMenu;
        if (menuBar != null)
        {
            // File menu
            var fileMenu = new NSMenu("File");
            fileMenu.AddItem(new NSMenuItem("New Chat", "n", (s, e) => { }));
            fileMenu.AddItem(new NSMenuItem("Open...", "o", (s, e) => { }));
            fileMenu.AddItem(NSMenuItem.SeparatorItem);
            fileMenu.AddItem(new NSMenuItem("Quit", "q", (s, e) => NSApplication.SharedApplication.Terminate(s)));

            // Edit menu
            var editMenu = new NSMenu("Edit");
            editMenu.AddItem(new NSMenuItem("Undo", "z", (s, e) => { }));
            editMenu.AddItem(new NSMenuItem("Redo", "Z", (s, e) => { }));
            editMenu.AddItem(NSMenuItem.SeparatorItem);
            editMenu.AddItem(new NSMenuItem("Cut", "x", (s, e) => { }));
            editMenu.AddItem(new NSMenuItem("Copy", "c", (s, e) => { }));
            editMenu.AddItem(new NSMenuItem("Paste", "v", (s, e) => { }));

            // Window menu
            var windowMenu = new NSMenu("Window");
            windowMenu.AddItem(new NSMenuItem("Minimize", "m", (s, e) => { }));
            windowMenu.AddItem(new NSMenuItem("Zoom", "", (s, e) => { }));
        }
    }

    private static void ConfigureWindowManagement()
    {
        // Enable multi-window support
        NSApplication.SharedApplication.SetActivationPolicy(NSApplicationActivationPolicy.Regular);
    }

    private static void ConfigureKeyboardShortcuts()
    {
        // Register keyboard shortcuts
        // Cmd+N = New Chat
        // Cmd+S = Save
        // Cmd+W = Close Window
        // Cmd+Q = Quit
        // Cmd+, = Preferences
    }

    /// <summary>Enable menu bar tray icon.</summary>
    public static void EnableTrayIcon()
    {
        // Create status bar item
        var statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(24);
        statusItem.Title = "FairyAI";
        statusItem.HighlightMode = true;

        var menu = new NSMenu();
        menu.AddItem(new NSMenuItem("Show", (s, e) => { }));
        menu.AddItem(new NSMenuItem("Hide", (s, e) => { }));
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem("Quit", (s, e) => NSApplication.SharedApplication.Terminate(s)));
        statusItem.Menu = menu;
    }
}
