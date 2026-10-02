using FairyAI_Android.Services;

namespace FairyAI_Android.Pages;

public partial class DeviceAdaptivePage : ContentPage
{
    private readonly DeviceAdaptationService _deviceService;

    public DeviceAdaptivePage()
    {
        InitializeComponent();
        _deviceService = new DeviceAdaptationService();
        _deviceService.Initialize();
        AdaptToDevice();
    }

    private void AdaptToDevice()
    {
        var config = _deviceService.GetUIConfig();
        DeviceLabel.Text = $"[{_deviceService.CurrentDevice}]";

        // Show appropriate layout
        PhoneLayout.IsVisible = _deviceService.CurrentDevice == DeviceAdaptationService.DeviceType.Phone
            || _deviceService.CurrentDevice == DeviceAdaptationService.DeviceType.Unknown;
        TabletLayout.IsVisible = _deviceService.CurrentDevice == DeviceAdaptationService.DeviceType.Tablet
            || _deviceService.CurrentDevice == DeviceAdaptationService.DeviceType.Desktop;
        WatchLayout.IsVisible = _deviceService.CurrentDevice == DeviceAdaptationService.DeviceType.Watch;

        // Adjust font size
        ChatEditor.FontSize = config.FontSize;

        // Show/hide sidebar based on device
        if (config.ShowSidebar)
        {
            // Sidebar is part of TabletLayout
        }
    }

    private void OnSendClicked(object? sender, EventArgs e)
    {
        var text = PhoneLayout.IsVisible ? ChatEditor.Text : TabletChatEditor.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        // Send message
        ChatEditor.Text = "";
        TabletChatEditor.Text = "";
    }

    private void OnVoiceClicked(object? sender, EventArgs e)
    {
        // Voice input
    }

    private void OnNavChat(object? sender, EventArgs e)
    {
        // Navigate to chat
    }

    private void OnNavKnowledge(object? sender, EventArgs e)
    {
        // Navigate to knowledge base
    }

    private void OnNavSettings(object? sender, EventArgs e)
    {
        // Navigate to settings
    }
}
