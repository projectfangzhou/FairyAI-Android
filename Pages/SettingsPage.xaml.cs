using FairyAI_Android.Services;
using DeviceInfo = FairyAI_Android.Services.DeviceInfo;

namespace FairyAI_Android.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ILocalSendService _localSend;
    private readonly ITtsApiService _apiTts;
    private List<DeviceInfo> _devices = new();
    private DeviceInfo? _selectedDevice;

    public SettingsPage(ILocalSendService localSend, ITtsApiService apiTts)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsPage XAML error: {ex}");
        }

        _localSend = localSend;
        _apiTts = apiTts;

        try
        {
            LanguagePicker.Items.Add("zh-CN");
            LanguagePicker.Items.Add("en-US");
            LanguagePicker.Items.Add("ja-JP");

            foreach (var provider in ConfigManager.LLMProviders.Keys)
                LLMProviderPicker.Items.Add(provider);
            foreach (var provider in ConfigManager.TTSProviders.Keys)
                TTSProviderPicker.Items.Add(provider);

            // Voice clone language
            VoiceCloneLangPicker.Items.Add("zh");
            VoiceCloneLangPicker.Items.Add("en");
            VoiceCloneLangPicker.Items.Add("ja");
            VoiceCloneLangPicker.SelectedIndex = 0;

            // Performance mode
            PerformanceModePicker.Items.Add("高性能");
            PerformanceModePicker.Items.Add("均衡");
            PerformanceModePicker.Items.Add("低占用");
            PerformanceModePicker.SelectedIndex = 1;

            // Fallback LLM providers
            foreach (var provider in ConfigManager.LLMProviders.Keys)
                FallbackLLMProviderPicker.Items.Add(provider);

            LoadConfig();

            DevicesList.SelectionChanged += OnDeviceSelected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsPage init error: {ex}");
        }
    }

    private void LoadConfig()
    {
        try
        {
            var config = ConfigManager.Load();
            PersonalityNameEntry.Text = config.Personality.Name ?? "Fairy";
            PersonalityPromptEditor.Text = config.Personality.SystemPrompt ?? "";
            LanguagePicker.SelectedIndex = config.Personality.Language switch { "en-US" => 1, "ja-JP" => 2, _ => 0 };
            EnableEmotionCheckBox.IsChecked = config.Personality.EnableEmotion;
            var llmIdx = Array.IndexOf(ConfigManager.LLMProviders.Keys.ToArray(), config.LLM.Provider);
            LLMProviderPicker.SelectedIndex = llmIdx >= 0 ? llmIdx : 0;
            LLMApiKeyEntry.Text = config.LLM.ApiKey;
            var ttsIdx = Array.IndexOf(ConfigManager.TTSProviders.Keys.ToArray(), config.TTS.Provider);
            TTSProviderPicker.SelectedIndex = ttsIdx >= 0 ? ttsIdx : 0;
            TTSApiKeyEntry.Text = config.TTS.ApiKey;
            UpdateTTSVoices(config.TTS.Provider);
            if (!string.IsNullOrEmpty(config.TTS.Voice))
            { var vi = TTSVoicePicker.Items.IndexOf(config.TTS.Voice); if (vi >= 0) TTSVoicePicker.SelectedIndex = vi; }
            DeviceNameEntry.Text = config.Sync.DeviceName ?? "FairyAI-Android";

            // Wake word
            var ww = config.WakeWord;
            WakeWordEnabledCheckBox.IsChecked = ww.Enabled;
            WakeWordEntry.Text = string.IsNullOrEmpty(ww.WakeWord) ? "小精灵" : ww.WakeWord;
            WakeWordAltEntry.Text = ww.AlternateWords != null ? string.Join(",", ww.AlternateWords) : "";
            WakeWordTtsCheckBox.IsChecked = ww.UseTtsFeedback;
            WakeWordFeedbackEntry.Text = string.IsNullOrEmpty(ww.FeedbackText) ? "我在" : ww.FeedbackText;
        }
        catch { }
    }

    private void OnTTSProviderChanged(object? sender, EventArgs e)
    { UpdateTTSVoices(TTSProviderPicker.SelectedItem?.ToString() ?? "System"); }

    private void UpdateTTSVoices(string provider)
    {
        TTSVoicePicker.Items.Clear();
        if (ConfigManager.TTSProviders.TryGetValue(provider, out var info) && info.Voices.Length > 0)
        { foreach (var v in info.Voices) TTSVoicePicker.Items.Add(v); TTSVoicePicker.SelectedIndex = 0; }
    }

    private async void OnTestTts(object? sender, EventArgs e)
    {
        var provider = TTSProviderPicker.SelectedItem?.ToString() ?? "System";
        if (provider == "System")
        { var tts = new Services.TtsService(_apiTts); await tts.SpeakAsync("你好, 我是 Fairy, 很高兴为你服务。"); return; }
        if (string.IsNullOrWhiteSpace(TTSApiKeyEntry.Text?.Trim())) { TtsStatus.Text = "请先输入 API Key"; return; }
        TtsStatus.Text = "合成中...";
        var voice = TTSVoicePicker.SelectedItem?.ToString();
        var audio = await _apiTts.SynthesizeAsync("你好, 我是 Fairy, 很高兴为你服务。", provider, voice);
        TtsStatus.Text = audio.Length > 0 ? $"合成成功 ({audio.Length} bytes)" : "合成失败";
        if (audio.Length > 0) { var tts = new Services.TtsService(_apiTts); await tts.SpeakAsync("你好, 我是 Fairy, 很高兴为你服务。"); }
    }

    private void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedDevice = e.CurrentSelection.FirstOrDefault() as DeviceInfo;
    }

    private async void OnStartLocalSend(object? sender, EventArgs e)
    {
        try
        {
            if (_localSend.IsRunning)
            { await _localSend.StopAsync(); StartLocalSendBtn.Text = "启动发现服务"; LocalSendStatus.Text = "已停止"; }
            else
            { await _localSend.StartAsync(); StartLocalSendBtn.Text = "停止发现服务"; LocalSendStatus.Text = "运行中..."; _ = DiscoverDevicesLoop(); }
        }
        catch (Exception ex) { LocalSendStatus.Text = $"启动失败: {ex.Message}"; }
    }

    private async void OnDiscoverDevices(object? sender, EventArgs e)
    {
        if (!_localSend.IsRunning) { LocalSendStatus.Text = "请先启动发现服务"; return; }
        LocalSendStatus.Text = "搜索中...";
        _devices = await _localSend.GetDevicesAsync();
        DevicesList.ItemsSource = _devices;
        LocalSendStatus.Text = $"找到 {_devices.Count} 台设备";
    }

    private async void OnAddRemoteDevice(object? sender, EventArgs e)
    {
        var input = RemoteDeviceEntry.Text?.Trim();
        if (string.IsNullOrEmpty(input)) { LocalSendStatus.Text = "请输入 IP/域名"; return; }
        LocalSendStatus.Text = $"连接 {input}...";
        string host = input.Contains(':') ? input.Split(':')[0] : input;
        int port = 9877; if (input.Contains(':')) int.TryParse(input.Split(':')[1], out port);
        await _localSend.AddKnownDeviceAsync(host, port);
        _devices = await _localSend.GetDevicesAsync();
        DevicesList.ItemsSource = _devices;
        RemoteDeviceEntry.Text = "";
        LocalSendStatus.Text = $"已添加, 共 {_devices.Count} 台设备";
    }

    private async Task DiscoverDevicesLoop()
    {
        while (_localSend.IsRunning)
        {
            try { _devices = await _localSend.GetDevicesAsync(); MainThread.BeginInvokeOnMainThread(() => { DevicesList.ItemsSource = _devices; if (_devices.Count > 0) LocalSendStatus.Text = $"找到 {_devices.Count} 台设备"; }); } catch { }
            await Task.Delay(5000);
        }
    }

    private async void OnPairDevice(object? sender, EventArgs e)
    {
        if (_selectedDevice == null) { LocalSendStatus.Text = "请先选择设备"; return; }
        var code = PairingCodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length != 6) { LocalSendStatus.Text = "请输入 6 位配对码"; return; }
        LocalSendStatus.Text = $"正在配对 {_selectedDevice.DeviceName}...";
        var ok = await _localSend.PairWithDeviceAsync(_selectedDevice, code);
        LocalSendStatus.Text = ok ? $"已配对 {_selectedDevice.DeviceName}" : "配对失败, 请检查配对码";
    }

    private async void OnSyncDevice(object? sender, EventArgs e)
    {
        if (_selectedDevice == null) { LocalSendStatus.Text = "请先选择设备"; return; }
        var code = PairingCodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(code)) { LocalSendStatus.Text = "请输入配对码"; return; }
        LocalSendStatus.Text = $"正在同步 {_selectedDevice.DeviceName} 的配置...";
        var configJson = await _localSend.RequestSyncConfigAsync(_selectedDevice, code);
        if (configJson != null)
        { var config = ConfigManager.Load(); config = ConfigManager.ApplySyncedConfig(config, configJson); ConfigManager.Save(config); LoadConfig(); LocalSendStatus.Text = $"同步完成! 人格: {config.Personality.Name}"; }
        else { LocalSendStatus.Text = "同步失败, 可能需要先配对"; }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LLMModelNameEntry.Text))
        {
            await DisplayAlert("模型名称必填", "请填写文本模型名称", "确定");
            return;
        }

        var config = ConfigManager.Load();
        config.Personality.Name = PersonalityNameEntry.Text?.Trim() ?? "Fairy";
        config.Personality.SystemPrompt = PersonalityPromptEditor.Text?.Trim() ?? "";
        config.Personality.Language = LanguagePicker.SelectedItem?.ToString() switch { "en-US" => "en-US", "ja-JP" => "ja-JP", _ => "zh-CN" };
        config.Personality.EnableEmotion = EnableEmotionCheckBox.IsChecked;
        var llmP = LLMProviderPicker.SelectedItem?.ToString() ?? "Kimi";
        config.LLM.Provider = llmP; config.LLM.ApiKey = LLMApiKeyEntry.Text?.Trim() ?? "";
        config.LLM.Model = LLMModelNameEntry.Text?.Trim() ?? "";
        if (ConfigManager.LLMProviders.TryGetValue(llmP, out var lp)) { config.LLM.BaseUrl = lp.BaseUrl; }
        config.FallbackLLM.ApiKey = FallbackLLMApiKeyEntry.Text?.Trim() ?? "";
        var ttsP = TTSProviderPicker.SelectedItem?.ToString() ?? "System";
        config.TTS.Provider = ttsP; config.TTS.ApiKey = TTSApiKeyEntry.Text?.Trim() ?? "";
        config.TTS.Model = TTSModelNameEntry.Text?.Trim() ?? "";
        config.TTS.Voice = TTSVoicePicker.SelectedItem?.ToString() ?? "";
        config.TTS.VoiceCloneAudioPath = VoiceClonePathEntry.Text?.Trim() ?? "";
        config.TTS.VoiceClonePromptText = VoiceClonePromptEntry.Text?.Trim() ?? "";
        config.TTS.VoiceCloneLang = VoiceCloneLangPicker.SelectedItem?.ToString() ?? "zh";
        if (ConfigManager.TTSProviders.TryGetValue(ttsP, out var tp)) { config.TTS.BaseUrl = tp.BaseUrl; }
        config.Context.Enabled = ContextCompressionCheckBox.IsChecked;
        config.Performance.Mode = PerformanceModePicker.SelectedIndex switch { 0 => "high", 1 => "balanced", 2 => "low", _ => "balanced" };
        config.Sync.SignalRUrl = RelayUrlEntry.Text?.Trim() ?? "";
        config.Sync.DeviceName = DeviceNameEntry.Text?.Trim() ?? "FairyAI-Android";

        // Wake word
        config.WakeWord = new WakeWordConfig
        {
            Enabled = WakeWordEnabledCheckBox.IsChecked,
            WakeWord = string.IsNullOrWhiteSpace(WakeWordEntry.Text) ? "小精灵" : WakeWordEntry.Text!.Trim(),
            AlternateWords = string.IsNullOrWhiteSpace(WakeWordAltEntry.Text)
                ? Array.Empty<string>()
                : WakeWordAltEntry.Text!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            UseTtsFeedback = WakeWordTtsCheckBox.IsChecked,
            FeedbackText = string.IsNullOrWhiteSpace(WakeWordFeedbackEntry.Text) ? "我在" : WakeWordFeedbackEntry.Text!.Trim()
        };

        ConfigManager.Save(config);
        await DisplayAlertAsync("设置", "已保存", "确定");
    }

    private async void OnTestClicked(object? sender, EventArgs e)
    {
        var config = ConfigManager.Load();
        if (string.IsNullOrWhiteSpace(config.LLM.ApiKey)) { await DisplayAlertAsync("错误", "请先配置 LLM API Key", "确定"); return; }
        try
        {
            var llm = new LlmService();
            var r = await llm.ChatAsync(new List<Models.ChatMessage> { new() { Role = "user", Content = "Say hello" } }, "hello");
            await DisplayAlertAsync("Test", r, "OK");
        }
        catch (Exception ex) { await DisplayAlertAsync("Error", ex.Message, "OK"); }
    }

    private async void OnBiliLogin(object? sender, EventArgs e)
    {
        BiliLoginStatus.Text = "正在获取二维码...";
        var biliService = new BiliLoginService();
        var (url, message) = await biliService.GetQRAsync();
        BiliLoginStatus.Text = message;
    }

    private async void OnImportDocument(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync();
            if (result != null)
            {
                var kb = new KnowledgeBaseService();
                var text = await kb.AnalyzeDocumentAsync(result.FullPath);
                KnowledgeStatus.Text = $"已导入: {result.FileName}";
            }
        }
        catch (Exception ex)
        {
            KnowledgeStatus.Text = $"错误: {ex.Message}";
        }
    }

    private void OnEnableAccessibility(object? sender, EventArgs e)
    {
        var a11y = new AccessibilityService();
        a11y.RequestPermission();
        ScreenStatus.Text = "已打开无障碍设置";
    }
}
