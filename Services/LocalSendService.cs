using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FairyAI_Android.Services;

public interface ILocalSendService : IAsyncDisposable
{
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
    Task<List<DeviceInfo>> GetDevicesAsync();
    Task AddKnownDeviceAsync(string hostOrIp, int port = 9877);
    Task RemoveKnownDeviceAsync(string deviceId);
    List<DeviceInfo> GetKnownDevices();
    Task SendFileAsync(string filePath, DeviceInfo device, IProgress<FileTransferProgress>? progress = null, CancellationToken ct = default);
    Task SendTextAsync(string text, string fileName, DeviceInfo device, CancellationToken ct = default);
    Task<bool> PairWithDeviceAsync(DeviceInfo device, string pairingCode, CancellationToken ct = default);
    Task<string?> RequestSyncConfigAsync(DeviceInfo device, string pairingCode, CancellationToken ct = default);
}

public class DeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Protocol { get; set; } = "http";
    public bool IsRemote { get; set; }
    public bool IsPaired { get; set; }
}

public class FileTransferProgress
{
    public string FileName { get; set; } = "";
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double Percent => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
}

public class LocalSendService : ILocalSendService
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly HttpClient _http;
    private readonly string _logPath;
    private readonly string _knownDevicesPath;
    private readonly List<DeviceInfo> _devices = new();

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public LocalSendService()
    {
        try
        {
            _logPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");
            _knownDevicesPath = Path.Combine(FileSystem.AppDataDirectory, "known_devices.json");
        }
        catch
        {
            _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fairy.log");
            _knownDevicesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "known_devices.json");
        }
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try { LoadKnownDevices(); } catch { }
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 53317));

            _ = BroadcastDiscoveryAsync(_cts.Token);
            _ = ListenDiscoveryAsync(_cts.Token);
            _ = ScanKnownDevicesLoopAsync(_cts.Token);

            Log("LocalSend service started");
        }
        catch (Exception ex)
        {
            Log($"LocalSend start error: {ex.Message}");
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        try { _udpClient?.Close(); } catch { }
        _udpClient = null;
        Log("LocalSend service stopped");
        return Task.CompletedTask;
    }

    public async Task<List<DeviceInfo>> GetDevicesAsync()
    {
        await BroadcastDiscoveryAsync(CancellationToken.None);
        await ScanKnownDevicesAsync();
        await Task.Delay(500);
        return _devices.ToList();
    }

    public async Task AddKnownDeviceAsync(string hostOrIp, int port = 9877)
    {
        try
        {
            IPAddress? ip;
            if (!IPAddress.TryParse(hostOrIp, out ip))
            {
                var addresses = await Dns.GetHostAddressesAsync(hostOrIp);
                ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            if (ip == null) { Log($"Cannot resolve: {hostOrIp}"); return; }

            var endpoint = $"http://{ip}:{port}";
            var deviceName = hostOrIp;
            try
            {
                var response = await _http.GetStringAsync($"{endpoint}/announce");
                var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("deviceName", out var name))
                    deviceName = name.GetString() ?? hostOrIp;
            }
            catch { }

            var deviceId = $"remote_{ip}:{port}";
            var existing = _devices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (existing == null)
            {
                _devices.Add(new DeviceInfo
                {
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    Endpoint = endpoint,
                    Protocol = "http",
                    IsRemote = true
                });
                SaveKnownDevices();
                Log($"Known device added: {deviceName} ({endpoint})");
            }
        }
        catch (Exception ex) { Log($"AddKnownDevice error: {ex.Message}"); }
    }

    public async Task RemoveKnownDeviceAsync(string deviceId)
    {
        _devices.RemoveAll(d => d.DeviceId == deviceId);
        SaveKnownDevices();
        await Task.CompletedTask;
    }

    public List<DeviceInfo> GetKnownDevices() => _devices.Where(d => d.IsRemote).ToList();

    public async Task SendFileAsync(string filePath, DeviceInfo device, IProgress<FileTransferProgress>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) { Log("SendFile: file not found"); return; }

        try
        {
            var fileName = Path.GetFileName(filePath);
            var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
            var totalBytes = fileBytes.Length;

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.Add("X-File-Name", fileName);
            content.Headers.Add("X-From-Device", ConfigManager.Load().Sync.DeviceName);
            content.Headers.Add("X-File-Size", totalBytes.ToString());

            progress?.Report(new FileTransferProgress { FileName = fileName, TotalBytes = totalBytes, BytesTransferred = 0 });

            var response = await _http.PostAsync($"{device.Endpoint}/send", content, ct);
            response.EnsureSuccessStatusCode();

            progress?.Report(new FileTransferProgress { FileName = fileName, TotalBytes = totalBytes, BytesTransferred = totalBytes });
            Log($"File sent: {fileName} ({totalBytes} bytes) to {device.DeviceName}");
        }
        catch (Exception ex) { Log($"SendFile error: {ex.Message}"); }
    }

    public async Task SendTextAsync(string text, string fileName, DeviceInfo device, CancellationToken ct = default)
    {
        try
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            using var content = new ByteArrayContent(textBytes);
            content.Headers.Add("X-File-Name", fileName);
            content.Headers.Add("X-From-Device", ConfigManager.Load().Sync.DeviceName);
            content.Headers.Add("Content-Type", "text/plain");

            var response = await _http.PostAsync($"{device.Endpoint}/send", content, ct);
            response.EnsureSuccessStatusCode();
            Log($"Text sent: {fileName} to {device.DeviceName}");
        }
        catch (Exception ex) { Log($"SendText error: {ex.Message}"); }
    }

    /// <summary>
    /// Pair with a device using a 6-digit verification code.
    /// Sends the code to the PC's /pair endpoint for verification.
    /// </summary>
    public async Task<bool> PairWithDeviceAsync(DeviceInfo device, string pairingCode, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { code = pairingCode });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            content.Headers.Add("X-From-Device", ConfigManager.Load().Sync.DeviceName);

            var response = await _http.PostAsync($"{device.Endpoint}/pair", content, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    device.IsPaired = true;
                    SaveKnownDevices();
                    Log($"Paired with {device.DeviceName}");
                    return true;
                }
            }
            Log($"Pair failed: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Log($"Pair error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Request synced config from PC after successful pairing.
    /// </summary>
    public async Task<string?> RequestSyncConfigAsync(DeviceInfo device, string pairingCode, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { code = pairingCode });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{device.Endpoint}/sync/config", content, ct);
            if (response.IsSuccessStatusCode)
            {
                var configJson = await response.Content.ReadAsStringAsync(ct);
                Log($"Sync config received from {device.DeviceName}");
                return configJson;
            }
            Log($"Sync config failed: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Sync config error: {ex.Message}");
            return null;
        }
    }

    // === Private methods ===

    private async Task BroadcastDiscoveryAsync(CancellationToken ct)
    {
        if (_udpClient == null) return;
        try
        {
            var config = ConfigManager.Load();
            var info = new { deviceId = Environment.MachineName, deviceName = config.Sync.DeviceName, app = "FairyAI", version = "1.3.0A" };
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(info));
            foreach (var ep in new[] { new IPEndPoint(IPAddress.Broadcast, 53317), new IPEndPoint(IPAddress.Broadcast, 53318) })
            { try { await _udpClient.SendAsync(data, data.Length, ep); } catch { } }
        }
        catch { }
    }

    private async Task ListenDiscoveryAsync(CancellationToken ct)
    {
        if (_udpClient == null) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.Buffer));
                var root = doc.RootElement;
                var deviceId = root.TryGetProperty("deviceId", out var id) ? id.GetString() ?? "" : "";
                var deviceName = root.TryGetProperty("deviceName", out var name) ? name.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(deviceId) && deviceId != Environment.MachineName)
                    AddOrUpdateDevice(deviceId, deviceName, $"http://{result.RemoteEndPoint.Address}:9877");
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task ScanKnownDevicesLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ScanKnownDevicesAsync();
            try { await Task.Delay(10000, ct); } catch { break; }
        }
    }

    private async Task ScanKnownDevicesAsync()
    {
        foreach (var device in _devices.Where(d => d.IsRemote).ToList())
        {
            try
            {
                var response = await _http.GetStringAsync($"{device.Endpoint}/announce");
                var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("deviceName", out var name))
                    device.DeviceName = name.GetString() ?? device.DeviceName;
            }
            catch { }
        }
    }

    private void AddOrUpdateDevice(string deviceId, string deviceName, string endpoint)
    {
        var existing = _devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (existing == null)
        {
            _devices.Add(new DeviceInfo { DeviceId = deviceId, DeviceName = deviceName, Endpoint = endpoint, Protocol = "http" });
            Log($"Device discovered: {deviceName} ({deviceId})");
        }
        else { existing.Endpoint = endpoint; existing.DeviceName = deviceName; }
    }

    private void LoadKnownDevices()
    {
        try
        {
            if (File.Exists(_knownDevicesPath))
            {
                var saved = JsonSerializer.Deserialize<List<DeviceInfo>>(File.ReadAllText(_knownDevicesPath));
                if (saved != null) foreach (var d in saved) _devices.Add(d);
            }
        }
        catch { }
    }

    private void SaveKnownDevices()
    {
        try
        {
            var json = JsonSerializer.Serialize(_devices.Where(d => d.IsRemote).ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_knownDevicesPath, json);
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _http.Dispose();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [LocalSend] {msg}\n"); } catch { }
    }
}
