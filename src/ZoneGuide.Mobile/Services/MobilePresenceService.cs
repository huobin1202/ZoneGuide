using System.Threading;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Services;

public interface IMobilePresenceService
{
    Task StartAsync();
    Task StopAsync();
    void UpdateStatus(string? statusMessage);
}

public sealed class MobilePresenceService : IMobilePresenceService, IDisposable
{
    private const string DeviceIdKey = "anonymous_device_id";
    private const int HeartbeatIntervalMs = 5000;
    private readonly ApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private Timer? _heartbeatTimer;
    private string _sessionId = string.Empty;
    private string _deviceId = string.Empty;
    private string _statusMessage = "App active";
    private bool _isRunning;

    public MobilePresenceService(
        ApiService apiService,
        ISettingsService settingsService,
        ILocationService locationService,
        IGeofenceService geofenceService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        _locationService = locationService;
        _geofenceService = geofenceService;
    }

    public async Task StartAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_isRunning)
            {
                return;
            }

            await _settingsService.LoadAsync();
            _deviceId = await GetAnonymousDeviceIdAsync();
            _sessionId = Guid.NewGuid().ToString("N");
            _statusMessage = "App active";
            _isRunning = true;

            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), null, HeartbeatIntervalMs, HeartbeatIntervalMs);
        }
        finally
        {
            _lifecycleGate.Release();
        }

        await SendHeartbeatAsync();
    }

    public async Task StopAsync()
    {
        string sessionIdToClose = string.Empty;

        await _lifecycleGate.WaitAsync();
        try
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            sessionIdToClose = _sessionId;
            _statusMessage = "App closed";
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
            _sessionId = string.Empty;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            await _apiService.UploadMobileOfflineAsync(sessionIdToClose);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MobilePresence] Offline error: {ex.Message}");
        }
    }

    public void UpdateStatus(string? statusMessage)
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            return;
        }

        _statusMessage = statusMessage.Trim();
    }

    private async Task SendHeartbeatAsync()
    {
        if (!_isRunning || string.IsNullOrWhiteSpace(_sessionId))
        {
            return;
        }

        try
        {
            var location = _locationService.CurrentLocation;
            var nearestPoi = _geofenceService.NearestPOI;
            var hasLocationFix = location != null;

            await _apiService.UploadMobileHeartbeatAsync(new MobileLiveHeartbeatDto
            {
                SessionId = _sessionId,
                DeviceId = _deviceId,
                IsTracking = _locationService.IsTracking,
                HasLocationFix = hasLocationFix,
                Platform = DeviceInfo.Current.Platform.ToString(),
                AppVersion = AppInfo.Current.VersionString,
                PreferredLanguage = _settingsService.Settings.PreferredLanguage,
                Latitude = location?.Latitude ?? 0,
                Longitude = location?.Longitude ?? 0,
                Accuracy = location?.Accuracy ?? 0,
                Speed = location?.Speed,
                Heading = location?.Heading,
                Altitude = location?.Altitude,
                Timestamp = location?.Timestamp ?? DateTime.UtcNow,
                NearestPoiId = nearestPoi?.Id,
                NearestPoiName = nearestPoi?.Name,
                StatusMessage = _statusMessage
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MobilePresence] Heartbeat error: {ex.Message}");
        }
    }

    private async Task<string> GetAnonymousDeviceIdAsync()
    {
        var deviceId = await _settingsService.GetAsync<string>(DeviceIdKey);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString("N")[..16];
            await _settingsService.SetAsync(DeviceIdKey, deviceId);
        }

        return deviceId;
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _lifecycleGate.Dispose();
    }
}
