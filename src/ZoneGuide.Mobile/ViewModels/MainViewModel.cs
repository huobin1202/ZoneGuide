using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel chính - Quản lý GPS, Geofence, Narration
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Bán kính tìm POI gần trên màn hình Home/List
    /// </summary>
    private const double NearbyPoiRangeMeters = 240;
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;
    private readonly IPOIRepository _poiRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ISettingsService _settingsService;
    private readonly ApiService _apiService;

    private const string SessionIdKey = "mobile_session_id";
    private string _sessionId = string.Empty;
    private DateTime _lastLiveHeartbeatAtUtc = DateTime.MinValue;
    private const int LiveHeartbeatIntervalSeconds = 5;
    private Timer? _heartbeatTimer;
    private const int HeartbeatTimerIntervalMs = 5000; // Send heartbeat every 5 seconds
    private bool _isFirstLocationFix = true;
    private readonly HashSet<int> _playedPoiIdsInCurrentVisit = new();

    [ObservableProperty]
    private bool isTracking;

    [ObservableProperty]
    private LocationData? currentLocation;

    [ObservableProperty]
    private POI? nearestPOI;

    [ObservableProperty]
    private double? nearestDistance;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private NarrationQueueItem? currentNarration;

    [ObservableProperty]
    private double narrationProgress;

    [ObservableProperty]
    private string statusMessage = AppLocalizer.Instance.Translate("main_status_ready");

    public ObservableCollection<POI> NearbyPOIs { get; } = new();

    public MainViewModel(
        ILocationService locationService,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        IPOIRepository poiRepository,
        IAnalyticsRepository analyticsRepository,
        ISettingsService settingsService,
        ApiService apiService)
    {
        _locationService = locationService;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _poiRepository = poiRepository;
        _analyticsRepository = analyticsRepository;
        _settingsService = settingsService;
        _apiService = apiService;

        // Subscribe events
        _locationService.LocationChanged += OnLocationChanged;
        _geofenceService.GeofenceTriggered += OnGeofenceTriggered;
        _narrationService.NarrationStarted += OnNarrationStarted;
        _narrationService.NarrationCompleted += OnNarrationCompleted;
        _narrationService.NarrationStopped += OnNarrationStopped;
        _narrationService.ProgressUpdated += OnProgressUpdated;
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        
        // Tải POIs từ database
        var pois = await _poiRepository.GetActiveAsync();
        _geofenceService.AddPOIs(pois);

        _sessionId = Guid.NewGuid().ToString("N");

        // Start periodic heartbeat timer for live monitoring
        // This ensures heartbeat is sent even when device is stationary
        _heartbeatTimer = new Timer(
            async _ => await SendPeriodicHeartbeatAsync(),
            null,
            HeartbeatTimerIntervalMs,
            HeartbeatTimerIntervalMs);
    }

    /// <summary>
    /// Periodic heartbeat sent via timer - maintains session even when device is stationary
    /// </summary>
    private async Task SendPeriodicHeartbeatAsync()
    {
        if (CurrentLocation == null)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastLiveHeartbeatAtUtc).TotalSeconds >= LiveHeartbeatIntervalSeconds)
        {
            await SendLiveHeartbeatAsync(CurrentLocation);
        }
    }

    [RelayCommand]
    private async Task StartTracking()
    {
        var accuracy = _settingsService.Settings.GPSAccuracy;
        var started = await _locationService.StartTrackingAsync(accuracy);
        
        if (started)
        {
            IsTracking = true;
            StatusMessage = AppLocalizer.Instance.Translate("main_status_tracking");
        }
        else
        {
            StatusMessage = AppLocalizer.Instance.Translate("main_status_tracking_failed");
        }
    }

    [RelayCommand]
    private async Task StopTracking()
    {
        await _locationService.StopTrackingAsync();
        IsTracking = false;
        StatusMessage = AppLocalizer.Instance.Translate("main_status_tracking_stopped");
    }

    [RelayCommand]
    private async Task ToggleTracking()
    {
        if (IsTracking)
            await StopTracking();
        else
            await StartTracking();
    }

    [RelayCommand]
    private async Task PauseNarration()
    {
        await _narrationService.PauseAsync();
    }

    [RelayCommand]
    private async Task ResumeNarration()
    {
        await _narrationService.ResumeAsync();
    }

    [RelayCommand]
    private async Task StopNarration()
    {
        await _narrationService.StopAsync();
    }

    [RelayCommand]
    private async Task SkipNarration()
    {
        await _narrationService.SkipAsync();
    }

    [RelayCommand]
    private async Task PlayPOI(POI poi)
    {
        try
        {
            _playedPoiIdsInCurrentVisit.Add(poi.Id);
            _geofenceService.ResetCooldown(poi.Id);

            var item = CreateNarrationItem(poi, GeofenceEventType.Enter, 0);
            await _narrationService.PlayImmediatelyAsync(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainVM] PlayPOI error: {ex}");
        }
    }

    private async void OnLocationChanged(object? sender, LocationData location)
    {
        CurrentLocation = location;

        // On first location fix, initialize geofence states silently to prevent auto-play
        if (_isFirstLocationFix)
        {
            _isFirstLocationFix = false;
            _geofenceService.InitializeFromLocation(location);
        }

        // Xử lý Geofence
        await _geofenceService.ProcessLocationUpdateAsync(location);

        // Cập nhật POI gần nhất
        NearestPOI = _geofenceService.NearestPOI;
        NearestDistance = _geofenceService.NearestPOIDistance;

        // Cập nhật danh sách POI gần
        var nearby = _geofenceService.GetPOIsInRange(NearbyPoiRangeMeters);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NearbyPOIs.Clear();
            foreach (var poi in nearby.Take(5))
            {
                NearbyPOIs.Add(poi);
            }
        });

        // Lưu lịch sử vị trí (ẩn danh)
        await SaveLocationHistoryAsync(location);
        await SendLiveHeartbeatAsync(location);
    }

    private async void OnGeofenceTriggered(object? sender, GeofenceEvent evt)
    {
        var settings = _settingsService.Settings;

        switch (evt.EventType)
        {
            case GeofenceEventType.Enter:
                if (_playedPoiIdsInCurrentVisit.Contains(evt.POI.Id))
                {
                    break;
                }

                if (settings.AutoPlayOnEnter)
                {
                    StatusMessage = string.Format(
                        AppLocalizer.Instance.Translate("main_status_enter_region"),
                        evt.POI.Name);
                    var item = CreateNarrationItem(evt.POI, evt.EventType, evt.Distance);
                    await _narrationService.PlayImmediatelyAsync(item);
                    _playedPoiIdsInCurrentVisit.Add(evt.POI.Id);
                }
                break;

            case GeofenceEventType.Approach:
                if (settings.NotifyOnApproach)
                {
                    StatusMessage = string.Format(
                        AppLocalizer.Instance.Translate("main_status_approach_region"),
                        evt.POI.Name);
                    // Có thể thêm notification ở đây
                }
                break;

            case GeofenceEventType.Exit:
                _playedPoiIdsInCurrentVisit.Remove(evt.POI.Id);
                StatusMessage = string.Format(
                    AppLocalizer.Instance.Translate("main_status_exit_region"),
                    evt.POI.Name);
                break;
        }
    }

    private void OnNarrationStarted(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = true;
            CurrentNarration = item;
            StatusMessage = string.Format(
                AppLocalizer.Instance.Translate("main_status_now_playing"),
                item.POI.Name);
        });
    }

    private void OnNarrationCompleted(object? sender, NarrationQueueItem item)
    {
        // DO NOT reset cooldown when narration completes naturally.
        // Cooldown is only reset when user manually stops/skips narration,
        // or when user exits the POI region (handled by GeofenceService).

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            CurrentNarration = null;
            NarrationProgress = 0;
            StatusMessage = AppLocalizer.Instance.Translate("main_status_play_completed");
        });
    }

    private void OnNarrationStopped(object? sender, NarrationQueueItem item)
    {
        _geofenceService.ResetCooldown(item.POI.Id);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            CurrentNarration = null;
            NarrationProgress = 0;
            StatusMessage = AppLocalizer.Instance.Translate("main_status_play_stopped");
        });
    }

    private void OnProgressUpdated(object? sender, double progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NarrationProgress = progress;
        });
    }

    private NarrationQueueItem CreateNarrationItem(POI poi, GeofenceEventType triggerType, double distance)
    {
        var settings = _settingsService.Settings;
        
        // Tìm file offline nếu có
        var offlineAudioPath = Path.Combine(
            FileSystem.AppDataDirectory, 
            "offline", 
            poi.TourId?.ToString() ?? "general",
            $"audio_{poi.Id}.mp3");

        return new NarrationQueueItem
        {
            POI = poi,
            AudioPath = File.Exists(offlineAudioPath) ? offlineAudioPath : poi.AudioFilePath,
            AudioUrl = poi.AudioUrl,
            TTSText = poi.TTSScript,
            Language = settings.PreferredLanguage,
            Priority = poi.Priority,
            TriggerType = triggerType,
            TriggerDistance = distance
        };
    }

    private async Task SaveLocationHistoryAsync(LocationData location)
    {
        var history = new LocationHistory
        {
            AnonymousDeviceId = await GetAnonymousDeviceIdAsync(),
            SessionId = _sessionId,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Accuracy = location.Accuracy,
            Speed = location.Speed,
            Heading = location.Heading,
            Altitude = location.Altitude,
            Timestamp = location.Timestamp
        };

        await _analyticsRepository.InsertLocationAsync(history);
    }

    private async Task<string> GetAnonymousDeviceIdAsync()
    {
        var deviceId = await _settingsService.GetAsync<string>("anonymous_device_id");
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = Guid.NewGuid().ToString("N")[..16]; // Hash ngắn
            await _settingsService.SetAsync("anonymous_device_id", deviceId);
        }
        return deviceId;
    }

    private async Task SendLiveHeartbeatAsync(LocationData location)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastLiveHeartbeatAtUtc).TotalSeconds < LiveHeartbeatIntervalSeconds)
        {
            return;
        }

        _lastLiveHeartbeatAtUtc = now;

        try
        {
            var deviceId = await GetAnonymousDeviceIdAsync();
            var nearestPoi = _geofenceService.NearestPOI;

            await _apiService.UploadMobileHeartbeatAsync(new MobileLiveHeartbeatDto
            {
                SessionId = _sessionId,
                DeviceId = deviceId,
                IsTracking = IsTracking,
                Platform = DeviceInfo.Current.Platform.ToString(),
                AppVersion = AppInfo.Current.VersionString,
                PreferredLanguage = _settingsService.Settings.PreferredLanguage,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Accuracy = location.Accuracy,
                Speed = location.Speed,
                Heading = location.Heading,
                Altitude = location.Altitude,
                Timestamp = location.Timestamp,
                NearestPoiId = nearestPoi?.Id,
                NearestPoiName = nearestPoi?.Name,
                StatusMessage = StatusMessage
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainVM] Live heartbeat error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _locationService.LocationChanged -= OnLocationChanged;
        _geofenceService.GeofenceTriggered -= OnGeofenceTriggered;
        _narrationService.NarrationStarted -= OnNarrationStarted;
        _narrationService.NarrationCompleted -= OnNarrationCompleted;
        _narrationService.NarrationStopped -= OnNarrationStopped;
        _narrationService.ProgressUpdated -= OnProgressUpdated;
    }
}
