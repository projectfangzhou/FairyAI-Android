using Foundation;
using UIKit;

namespace FairyAI_Android.Platforms.iOS;

/// <summary>
/// iOS-specific adaptations for iPhone and iPad.
/// </summary>
public static class IOSAdaptation
{
    /// <summary>Configure iOS-specific UI and behavior.</summary>
    public static void Configure()
    {
        // Set status bar style
        UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.LightContent, true);

        // Configure navigation
        if (UIApplication.SharedApplication.KeyWindow != null)
        {
            var navBar = UIApplication.SharedApplication.KeyWindow.RootViewController?.NavigationController?.NavigationBar;
            if (navBar != null)
            {
                navBar.TintColor = UIColor.FromRGB(123, 104, 238); // #7b68ee
                navBar.BarTintColor = UIColor.FromRGB(26, 26, 46); // #1a1a2e
                navBar.TitleTextAttributes = new UIStringAttributes
                {
                    ForegroundColor = UIColor.White
                };
            }
        }
    }

    /// <summary>Enable iPad split-view support.</summary>
    public static void EnableSplitView()
    {
        if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
        {
            // Configure split-view for iPad
            UIApplication.SharedApplication.KeyWindow?.RootViewController?.SetNeedsStatusBarAppearanceUpdate();
        }
    }

    /// <summary>Enable Apple Pencil support on iPad.</summary>
    public static void EnablePencilSupport()
    {
        if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
        {
            // Enable Apple Pencil interactions
        }
    }

    /// <summary>Enable haptic feedback on iPhone.</summary>
    public static void EnableHapticFeedback()
    {
        if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone)
        {
            // Configure haptic feedback
        }
    }
}
