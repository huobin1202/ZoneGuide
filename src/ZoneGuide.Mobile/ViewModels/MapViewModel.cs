using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho Map View
/// </summary>
public partial class MapViewModel : ObservableObject
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly ITourRepository _tourRepository;
    private readonly ISyncService _syncService;

    [ObservableProperty]
    private Location? userLocation;

    [ObservableProperty]
    private MapSpan? mapSpan;

    [ObservableProperty]
    private POI? selectedPOI;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? searchQuery;

    [ObservableProperty]
    private string? selectedCategory;

    private List<POI> _allPOIs = new();

    public ObservableCollection<POI> POIs { get; } = new();
    public ObservableCollection<Pin> MapPins { get; } = new();

    public List<string> Categories { get; } = new()
    {
        "Tất cả",
        "Du lịch",
        "Dịch vụ",
        "Ăn uống",
        "Giải trí",
        "Mua sắm",
        "Khác"
    };

    public MapViewModel(
        ILocationService locationService,
        IGeofenceService geofenceService,
        IPOIRepository poiRepository,
        INarrationService narrationService,
        ITourRepository tourRepository,
        ISyncService syncService)
    {
        _locationService = locationService;
        _geofenceService = geofenceService;
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _tourRepository = tourRepository;
        _syncService = syncService;

        _locationService.LocationChanged += OnLocationChanged;
    }

    public async Task InitializeAsync()
    {
        if (IsLoading) return; // Tránh gọi nhiều lần
        
        IsLoading = true;
        
        try
        {
            // Đặt vị trí mặc định trước để map render ngay
            UserLocation = new Location(10.8231, 106.6297); // Hồ Chí Minh
            MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(1));

            // === Bước 1: Thử đồng bộ từ Server (Admin Web → API → Mobile) ===
            try
            {
                System.Diagnostics.Debug.WriteLine("[MapVM] Syncing from server...");
                await _syncService.SyncFromServerAsync();
                System.Diagnostics.Debug.WriteLine("[MapVM] Sync from server completed!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapVM] Server sync failed (non-fatal): {ex.Message}");
                // Nếu không kết nối được server → dùng dữ liệu local/seed
            }

            // === Bước 2: Nếu DB vẫn trống (server không có data hoặc offline) → seed mẫu ===
            await SeedDataService.SeedIfEmptyAsync(_poiRepository, _tourRepository);

            // === Bước 3: Tải POIs từ SQLite local ===
            await LoadPOIsAsync();

            // Sau đó cố lấy vị trí thực
            try
            {
                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null)
                {
                    UserLocation = new Location(location.Latitude, location.Longitude);
                    MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapVM] Location error (non-fatal): {ex.Message}");
                // Giữ vị trí mặc định, không crash
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[MapVM] InitializeAsync error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        try
        {
            var pois = await _poiRepository.GetActiveAsync();
            _allPOIs = pois;
            PopulatePins(_allPOIs);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void PopulatePins(List<POI> pois)
    {
        POIs.Clear();
        MapPins.Clear();

        foreach (var poi in pois)
        {
            POIs.Add(poi);
            
            var pin = new Pin
            {
                Label = poi.Name,
                Address = poi.ShortDescription,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            };
            
            MapPins.Add(pin);
        }
    }

    [RelayCommand]
    private void PerformSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) && string.IsNullOrWhiteSpace(SelectedCategory))
        {
            PopulatePins(_allPOIs);
            return;
        }

        var query = SearchQuery?.ToLowerInvariant();
        var results = _allPOIs.Where(p => 
            (string.IsNullOrWhiteSpace(query) || p.Name.ToLowerInvariant().Contains(query) || 
            (p.ShortDescription != null && p.ShortDescription.ToLowerInvariant().Contains(query))) &&
            (string.IsNullOrWhiteSpace(SelectedCategory) || SelectedCategory == "Tất cả" || p.Category == SelectedCategory))
            .ToList();

        PopulatePins(results);

        if (results.Count > 0)
        {
            SelectedPOI = results.First();
            MapSpan = MapSpan.FromCenterAndRadius(
                new Location(SelectedPOI.Latitude, SelectedPOI.Longitude), 
                Distance.FromKilometers(1)); // Zoom out a bit to show searched area
        }
    }

    [RelayCommand]
    private async Task CenterOnUser()
    {
        if (UserLocation != null)
        {
            MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
        }
        else
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                UserLocation = new Location(location.Latitude, location.Longitude);
                MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
            }
        }
    }

    [RelayCommand]
    private void SelectPOI(POI poi)
    {
        SelectedPOI = poi;
        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(poi.Latitude, poi.Longitude), 
            Distance.FromMeters(200));
    }

    [RelayCommand]
    private async Task PlaySelectedPOI()
    {
        if (SelectedPOI == null)
            return;

        var item = new NarrationQueueItem
        {
            POI = SelectedPOI,
            AudioPath = SelectedPOI.AudioFilePath,
            AudioUrl = SelectedPOI.AudioUrl,
            TTSText = SelectedPOI.TTSScript ?? SelectedPOI.FullDescription,
            Language = SelectedPOI.Language,
            Priority = SelectedPOI.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };

        await _narrationService.PlayImmediatelyAsync(item);
    }

    [RelayCommand]
    private async Task NavigateToPOI()
    {
        if (SelectedPOI == null)
            return;

        var location = new Location(SelectedPOI.Latitude, SelectedPOI.Longitude);
        var options = new MapLaunchOptions { NavigationMode = NavigationMode.Walking };

        await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(location, options);
    }

    [RelayCommand]
    private async Task ViewPOIDetail()
    {
        if (SelectedPOI == null)
            return;

        await Shell.Current.GoToAsync($"POIDetailPage?id={SelectedPOI.Id}");
    }

    private void OnLocationChanged(object? sender, LocationData location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UserLocation = new Location(location.Latitude, location.Longitude);
        });
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        PerformSearch();
    }

    public POI? FindNearestPOI()
    {
        return _geofenceService.NearestPOI;
    }

    public double? GetDistanceToNearestPOI()
    {
        return _geofenceService.NearestPOIDistance;
    }
}
