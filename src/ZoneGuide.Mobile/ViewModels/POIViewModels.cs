using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho danh sách POI
/// </summary>
public partial class POIListViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly ITourRepository _tourRepository;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;
    private readonly ISyncService _syncService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private POI? selectedPOI;

    public ObservableCollection<POI> POIs { get; } = new();
    public ObservableCollection<POI> FilteredPOIs { get; } = new();

    public POIListViewModel(
        IPOIRepository poiRepository,
        ITourRepository tourRepository,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        ISyncService syncService)
    {
        _poiRepository = poiRepository;
        _tourRepository = tourRepository;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _syncService = syncService;
    }

    public async Task InitializeAsync()
    {
        // === Bước 1: Thử đồng bộ từ Server ===
        try
        {
            System.Diagnostics.Debug.WriteLine("[POIListVM] Syncing from server...");
            await _syncService.SyncFromServerAsync();
            System.Diagnostics.Debug.WriteLine("[POIListVM] Sync completed!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIListVM] Server sync failed (non-fatal): {ex.Message}");
        }

        // === Bước 2: Seed nếu vẫn trống ===
        await SeedDataService.SeedIfEmptyAsync(_poiRepository, _tourRepository);

        // === Bước 3: Tải từ SQLite ===
        await LoadPOIsAsync();
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var pois = await _poiRepository.GetActiveAsync();
            
            POIs.Clear();
            FilteredPOIs.Clear();

            // Sắp xếp theo khoảng cách nếu có vị trí
            foreach (var poi in pois.OrderBy(p => p.Name))
            {
                POIs.Add(poi);
                FilteredPOIs.Add(poi);
            }
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadPOIsAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        FilteredPOIs.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var poi in POIs)
            {
                FilteredPOIs.Add(poi);
            }
        }
        else
        {
            var results = await _poiRepository.SearchAsync(SearchText);
            foreach (var poi in results)
            {
                FilteredPOIs.Add(poi);
            }
        }
    }

    [RelayCommand]
    private async Task ViewDetail(POI poi)
    {
        await Shell.Current.GoToAsync($"POIDetailPage?id={poi.Id}");
    }

    [RelayCommand]
    private async Task PlayPOI(POI poi)
    {
        var item = new NarrationQueueItem
        {
            POI = poi,
            AudioPath = poi.AudioFilePath,
            AudioUrl = poi.AudioUrl,
            TTSText = poi.TTSScript ?? poi.FullDescription,
            Language = poi.Language,
            Priority = poi.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };

        await _narrationService.PlayImmediatelyAsync(item);
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }
}

/// <summary>
/// ViewModel chi tiết POI
/// </summary>
[QueryProperty(nameof(POIIdString), "id")]
public partial class POIDetailViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly IGeofenceService _geofenceService;

    private string? _poiIdString;
    public string? POIIdString
    {
        get => _poiIdString;
        set
        {
            if (SetProperty(ref _poiIdString, value) && int.TryParse(value, out var id))
            {
                PoiId = id;
                _ = LoadPOIAsync();
            }
        }
    }

    [ObservableProperty]
    private int poiId;

    [ObservableProperty]
    private POI? currentPoi;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private double? distance;

    public POIDetailViewModel(
        IPOIRepository poiRepository,
        INarrationService narrationService,
        IGeofenceService geofenceService)
    {
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _geofenceService = geofenceService;

        _narrationService.NarrationStarted += (s, e) => IsPlaying = true;
        _narrationService.NarrationCompleted += (s, e) => IsPlaying = false;
        _narrationService.NarrationStopped += (s, e) => IsPlaying = false;
        _narrationService.ProgressUpdated += (s, p) => Progress = p;
    }

    private async Task LoadPOIAsync()
    {
        CurrentPoi = await _poiRepository.GetByIdAsync(PoiId);

        if (CurrentPoi != null && _geofenceService.NearestPOI?.Id == CurrentPoi.Id)
        {
            Distance = _geofenceService.NearestPOIDistance;
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (CurrentPoi == null)
            return;

        var item = new NarrationQueueItem
        {
            POI = CurrentPoi,
            AudioPath = CurrentPoi.AudioFilePath,
            AudioUrl = CurrentPoi.AudioUrl,
            TTSText = CurrentPoi.TTSScript ?? CurrentPoi.FullDescription,
            Language = CurrentPoi.Language,
            Priority = CurrentPoi.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };

        await _narrationService.PlayImmediatelyAsync(item);
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        await _narrationService.PauseAsync();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _narrationService.StopAsync();
    }

    [RelayCommand]
    private async Task NavigateAsync()
    {
        if (CurrentPoi == null)
            return;

        var location = new Location(CurrentPoi.Latitude, CurrentPoi.Longitude);
        var options = new MapLaunchOptions { NavigationMode = NavigationMode.Walking };
        await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(location, options);
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (CurrentPoi == null)
            return;

        await Share.RequestAsync(new ShareTextRequest
        {
            Title = CurrentPoi.Name,
            Text = $"{CurrentPoi.Name}\n{CurrentPoi.ShortDescription}\n{CurrentPoi.MapLink}"
        });
    }
}
