using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using System.Collections.ObjectModel;

namespace HeriStepAI.Mobile.ViewModels;

/// <summary>
/// ViewModel cho danh sách POI
/// </summary>
public partial class POIListViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;

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
        IGeofenceService geofenceService,
        INarrationService narrationService)
    {
        _poiRepository = poiRepository;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
    }

    public async Task InitializeAsync()
    {
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
                POIId = id;
                _ = LoadPOIAsync();
            }
        }
    }

    [ObservableProperty]
    private int poiId;

    [ObservableProperty]
    private POI? poi;

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
        POI = await _poiRepository.GetByIdAsync(POIId);

        if (POI != null && _geofenceService.NearestPOI?.Id == POI.Id)
        {
            Distance = _geofenceService.NearestPOIDistance;
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (POI == null)
            return;

        var item = new NarrationQueueItem
        {
            POI = POI,
            TTSText = POI.TTSScript ?? POI.FullDescription,
            Language = POI.Language,
            Priority = POI.Priority,
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
        if (POI == null)
            return;

        var location = new Location(POI.Latitude, POI.Longitude);
        var options = new MapLaunchOptions { NavigationMode = NavigationMode.Walking };
        await Map.Default.OpenAsync(location, options);
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (POI == null)
            return;

        await Share.RequestAsync(new ShareTextRequest
        {
            Title = POI.Name,
            Text = $"{POI.Name}\n{POI.ShortDescription}\n{POI.MapLink}"
        });
    }
}
