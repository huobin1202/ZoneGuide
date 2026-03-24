using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel chính - Quản lý GPS, Geofence, Narration
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;
    private readonly IPOIRepository _poiRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ISettingsService _settingsService;

    private string _sessionId = string.Empty;

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
    private string statusMessage = "Sẵn sàng";

    public ObservableCollection<POI> NearbyPOIs { get; } = new();

    public MainViewModel(
        ILocationService locationService,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        IPOIRepository poiRepository,
        IAnalyticsRepository analyticsRepository,
        ISettingsService settingsService)
    {
        _locationService = locationService;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _poiRepository = poiRepository;
        _analyticsRepository = analyticsRepository;
        _settingsService = settingsService;

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
    }

    [RelayCommand]
    private async Task StartTracking()
    {
        var accuracy = _settingsService.Settings.GPSAccuracy;
        var started = await _locationService.StartTrackingAsync(accuracy);
        
        if (started)
        {
            IsTracking = true;
            StatusMessage = "Đang theo dõi vị trí...";
        }
        else
        {
            StatusMessage = "Không thể bắt đầu theo dõi vị trí";
        }
    }

    [RelayCommand]
    private async Task StopTracking()
    {
        await _locationService.StopTrackingAsync();
        IsTracking = false;
        StatusMessage = "Đã dừng theo dõi";
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
        var item = CreateNarrationItem(poi, GeofenceEventType.Enter, 0);
        await _narrationService.PlayImmediatelyAsync(item);
    }

    private async void OnLocationChanged(object? sender, LocationData location)
    {
        CurrentLocation = location;

        // Xử lý Geofence
        await _geofenceService.ProcessLocationUpdateAsync(location);

        // Cập nhật POI gần nhất
        NearestPOI = _geofenceService.NearestPOI;
        NearestDistance = _geofenceService.NearestPOIDistance;

        // Cập nhật danh sách POI gần
        var nearby = _geofenceService.GetPOIsInRange(_settingsService.Settings.DefaultApproachRadius * 2);
        
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
    }

    private async void OnGeofenceTriggered(object? sender, GeofenceEvent evt)
    {
        var settings = _settingsService.Settings;

        switch (evt.EventType)
        {
            case GeofenceEventType.Enter:
                if (settings.AutoPlayOnEnter)
                {
                    StatusMessage = $"Đã vào vùng: {evt.POI.Name}";
                    var item = CreateNarrationItem(evt.POI, evt.EventType, evt.Distance);
                    await _narrationService.PlayImmediatelyAsync(item);
                }
                break;

            case GeofenceEventType.Approach:
                if (settings.NotifyOnApproach)
                {
                    StatusMessage = $"Đang đến gần: {evt.POI.Name}";
                    // Có thể thêm notification ở đây
                }
                break;

            case GeofenceEventType.Exit:
                StatusMessage = $"Đã rời khỏi: {evt.POI.Name}";
                break;
        }
    }

    private void OnNarrationStarted(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = true;
            CurrentNarration = item;
            StatusMessage = $"Đang phát: {item.POI.Name}";
        });

        // Lưu lịch sử narration
        _ = SaveNarrationHistoryAsync(item, true);
    }

    private void OnNarrationCompleted(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            CurrentNarration = null;
            NarrationProgress = 0;
            StatusMessage = "Hoàn thành phát";
        });

        // Cập nhật lịch sử narration
        _ = SaveNarrationHistoryAsync(item, false, true);
    }

    private void OnNarrationStopped(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPlaying = false;
            CurrentNarration = null;
            NarrationProgress = 0;
            StatusMessage = "Đã dừng phát";
        });

        _ = SaveNarrationHistoryAsync(item, false, false);
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
            TTSText = poi.TTSScript ?? poi.FullDescription,
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

    private async Task SaveNarrationHistoryAsync(NarrationQueueItem item, bool isStart, bool completed = false)
    {
        if (isStart)
        {
            var history = new NarrationHistory
            {
                AnonymousDeviceId = await GetAnonymousDeviceIdAsync(),
                SessionId = _sessionId,
                POIId = item.POI.Id,
                POIName = item.POI.Name,
                Language = item.Language,
                StartTime = DateTime.UtcNow,
                TriggerType = item.TriggerType.ToString(),
                TriggerDistance = item.TriggerDistance,
                TriggerLatitude = CurrentLocation?.Latitude ?? 0,
                TriggerLongitude = CurrentLocation?.Longitude ?? 0
            };

            await _analyticsRepository.InsertNarrationAsync(history);
            return;
        }

        var existing = (await _analyticsRepository.GetNarrationsBySessionAsync(_sessionId))
            .Where(h => h.POIId == item.POI.Id && h.EndTime == null)
            .OrderByDescending(h => h.StartTime)
            .FirstOrDefault();

        if (existing == null)
            return;

        var endedAt = DateTime.UtcNow;
        existing.EndTime = endedAt;
        existing.DurationSeconds = Math.Max(1, (int)Math.Round((endedAt - existing.StartTime).TotalSeconds));
        existing.TotalDurationSeconds = existing.DurationSeconds;
        existing.Completed = completed;

        await _analyticsRepository.UpdateNarrationAsync(existing);
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
}
