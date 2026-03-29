using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho Map View
/// </summary>
[QueryProperty(nameof(TourIdString), "tourId")]
[QueryProperty(nameof(StartTourString), "startTour")]
public partial class MapViewModel : ObservableObject
{
    private const double MaxAcceptedAccuracyMeters = 1500;
    private const double MaxAcceptedJumpKm = 80;
    private static readonly HttpClient RouteHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly ITourRepository _tourRepository;
    private readonly ISyncService _syncService;

    private bool _startTourRequested;
    private int? _requestedTourId;
    private bool _isTourModeActive;
    private bool _isHandlingGeofencePlayback;

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

    public string? TourIdString
    {
        get => _requestedTourId?.ToString();
        set
        {
            if (int.TryParse(value, out var parsedId))
            {
                _requestedTourId = parsedId;
            }
        }
    }

    public string? StartTourString
    {
        get => _startTourRequested ? "true" : "false";
        set
        {
            _startTourRequested = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetTourRequest(int? tourId, bool startTour)
    {
        _requestedTourId = tourId;
        _startTourRequested = startTour && tourId.HasValue;
    }

    public async Task ApplyTourRequestAsync()
    {
        if (!_startTourRequested || !_requestedTourId.HasValue)
            return;

        await ApplyStartTourRouteIfRequestedAsync(_requestedTourId.Value);
    }

    private List<POI> _allPOIs = new();

    public ObservableCollection<POI> POIs { get; } = new();
    public ObservableCollection<Pin> MapPins { get; } = new();
    public ObservableCollection<Location> TourRoutePoints { get; } = new();

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
        _geofenceService.GeofenceTriggered += OnGeofenceTriggered;
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

            await ApplyTourRequestAsync();

            // Sau đó cố lấy vị trí thực
            try
            {
                if (!_locationService.IsTracking)
                {
                    await _locationService.StartTrackingAsync(GPSAccuracyLevel.Medium);
                }

                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null && IsValidLocation(location))
                {
                    var candidate = new Location(location.Latitude, location.Longitude);

                    if (!IsLocationOutlier(candidate))
                    {
                        UserLocation = candidate;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MapVM] Ignored startup outlier location: {location.Latitude},{location.Longitude} (acc={location.Accuracy:F0}m)");
                    }

                    // Ưu tiên hiển thị POI trên bản đồ. Chỉ zoom vào vị trí user khi không có POI.
                    if (TourRoutePoints.Count > 1)
                    {
                        ApplyMapSpanForTourAndUser(POIs.ToList());
                    }
                    else if (_allPOIs.Count > 0)
                    {
                        ApplyMapSpanForPoiCollection(_allPOIs);
                    }
                    else
                    {
                        MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MapVM] Current location is null/invalid, keeping POI/default center.");
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
            var pois = (await _poiRepository.GetActiveAsync())
                .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180)
                .ToList();

            _allPOIs = pois;
            PopulatePins(_allPOIs);
            if (!_isTourModeActive)
            {
                SetMonitoredPois(_allPOIs);
            }

            System.Diagnostics.Debug.WriteLine($"[MapVM] Loaded POIs: {pois.Count}");

            // Nếu chưa có vị trí người dùng, tự focus vào cụm POI để người dùng luôn thấy marker.
            if (pois.Count > 0)
            {
                ApplyMapSpanForPoiCollection(pois);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadPOIsAsync error: {ex}");
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

    private async Task ApplyStartTourRouteIfRequestedAsync(int tourId)
    {
        var tourPois = (await _poiRepository.GetByTourIdAsync(tourId))
            .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180)
            .OrderBy(p => p.OrderInTour)
            .ToList();

        if (tourPois.Count == 0)
        {
            ClearTourRoute();
            _startTourRequested = false;
            _requestedTourId = null;
            return;
        }

        PopulatePins(tourPois);
        SetMonitoredPois(tourPois);
        _isTourModeActive = true;

        await SetTourRouteAsync(tourPois);
        SelectedPOI = tourPois.First();
        ApplyMapSpanForTourAndUser(tourPois);

        _startTourRequested = false;
        _requestedTourId = null;
    }

    private async Task SetTourRouteAsync(List<POI> tourPois)
    {
        var routePoints = await BuildRoadRouteAsync(tourPois);

        if (routePoints.Count < 2)
        {
            routePoints = tourPois
                .Select(p => new Location(p.Latitude, p.Longitude))
                .ToList();
        }

        TourRoutePoints.Clear();

        foreach (var point in routePoints)
        {
            TourRoutePoints.Add(point);
        }
    }

    private static async Task<List<Location>> BuildRoadRouteAsync(List<POI> tourPois)
    {
        var result = new List<Location>();

        if (tourPois.Count == 0)
            return result;

        if (tourPois.Count == 1)
        {
            result.Add(new Location(tourPois[0].Latitude, tourPois[0].Longitude));
            return result;
        }

        for (var i = 0; i < tourPois.Count - 1; i++)
        {
            var from = new Location(tourPois[i].Latitude, tourPois[i].Longitude);
            var to = new Location(tourPois[i + 1].Latitude, tourPois[i + 1].Longitude);

            var segment = await GetRoadSegmentAsync(from, to);
            if (segment.Count == 0)
            {
                if (result.Count == 0 || !IsSamePoint(result[^1], from))
                {
                    result.Add(from);
                }

                result.Add(to);
                continue;
            }

            if (result.Count > 0 && segment.Count > 0 && IsSamePoint(result[^1], segment[0]))
            {
                segment.RemoveAt(0);
            }

            result.AddRange(segment);
        }

        return result;
    }

    private static async Task<List<Location>> GetRoadSegmentAsync(Location from, Location to)
    {
        try
        {
            var requestUri = string.Create(
                CultureInfo.InvariantCulture,
                $"https://router.project-osrm.org/route/v1/driving/{from.Longitude},{from.Latitude};{to.Longitude},{to.Latitude}?overview=full&geometries=geojson");

            using var response = await RouteHttpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
                return new List<Location>();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            if (!json.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                return new List<Location>();
            }

            var coordinates = routes[0].GetProperty("geometry").GetProperty("coordinates");

            var points = new List<Location>();
            foreach (var coordinate in coordinates.EnumerateArray())
            {
                if (coordinate.GetArrayLength() < 2)
                    continue;

                var lon = coordinate[0].GetDouble();
                var lat = coordinate[1].GetDouble();
                points.Add(new Location(lat, lon));
            }

            return points;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] GetRoadSegmentAsync failed: {ex.Message}");
            return new List<Location>();
        }
    }

    private static bool IsSamePoint(Location a, Location b)
    {
        const double epsilon = 0.00001;
        return Math.Abs(a.Latitude - b.Latitude) < epsilon &&
               Math.Abs(a.Longitude - b.Longitude) < epsilon;
    }

    private void ClearTourRoute()
    {
        TourRoutePoints.Clear();
    }

    [RelayCommand]
    private void PerformSearch()
    {
        ClearTourRoute();
        _isTourModeActive = false;
        SetMonitoredPois(_allPOIs);

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
            if (location != null && IsValidLocation(location))
            {
                var candidate = new Location(location.Latitude, location.Longitude);

                if (!IsLocationOutlier(candidate))
                {
                    UserLocation = candidate;
                    MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
                }
                else
                {
                    ErrorMessage = "Vị trí hiện tại chưa chính xác. Vui lòng thử lại.";
                    System.Diagnostics.Debug.WriteLine(
                        $"[MapVM] Ignored center-on-user outlier: {location.Latitude},{location.Longitude} (acc={location.Accuracy:F0}m)");

                    if (_allPOIs.Count > 0)
                    {
                        ApplyMapSpanForPoiCollection(_allPOIs);
                    }
                }
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

        _geofenceService.ResetCooldown(SelectedPOI.Id);

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

    [RelayCommand]
    private void DismissSelectedPOI()
    {
        SelectedPOI = null;

        if (TourRoutePoints.Count > 1)
            return;

        ShowAllMarkers();
    }

    [RelayCommand]
    private void ShowAllMarkers()
    {
        SelectedPOI = null;
        ClearTourRoute();
        _isTourModeActive = false;
        SetMonitoredPois(_allPOIs);
        PopulatePins(_allPOIs);

        if (_allPOIs.Count > 0)
        {
            ApplyMapSpanForPoiCollection(_allPOIs, forceAll: true);
        }
    }

    private void OnLocationChanged(object? sender, LocationData location)
    {
        if (!IsValidLocation(location))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MapVM] Ignored invalid location update: {location.Latitude},{location.Longitude} (acc={location.Accuracy:F0}m)");
            return;
        }

        var candidate = new Location(location.Latitude, location.Longitude);

        if (IsLocationOutlier(candidate))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MapVM] Ignored outlier location update: {location.Latitude},{location.Longitude} (acc={location.Accuracy:F0}m)");
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UserLocation = candidate;
        });

        _ = _geofenceService.ProcessLocationUpdateAsync(location);
    }

    private async void OnGeofenceTriggered(object? sender, GeofenceEvent evt)
    {
        if (evt.EventType != GeofenceEventType.Enter)
            return;

        if (_isHandlingGeofencePlayback)
            return;

        if (_narrationService.CurrentItem?.POI.Id == evt.POI.Id &&
            (_narrationService.IsPlaying || _narrationService.IsPaused))
        {
            return;
        }

        // Không ngắt audio đang phát dở để tránh trải nghiệm bị giật.
        if (_narrationService.IsPlaying)
            return;

        _isHandlingGeofencePlayback = true;

        try
        {
            await _narrationService.PlayImmediatelyAsync(new NarrationQueueItem
            {
                POI = evt.POI,
                AudioPath = evt.POI.AudioFilePath,
                AudioUrl = evt.POI.AudioUrl,
                TTSText = evt.POI.TTSScript ?? evt.POI.FullDescription,
                Language = evt.POI.Language,
                Priority = evt.POI.Priority,
                TriggerType = evt.EventType,
                TriggerDistance = evt.Distance
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnGeofenceTriggered play error: {ex.Message}");
        }
        finally
        {
            _isHandlingGeofencePlayback = false;
        }
    }

    private static bool IsValidLocation(LocationData location)
    {
        var hasValidCoordinates = location.Latitude is >= -90 and <= 90 &&
                                  location.Longitude is >= -180 and <= 180 &&
                                  !(Math.Abs(location.Latitude) < 0.0001 && Math.Abs(location.Longitude) < 0.0001);

        var hasAcceptableAccuracy = location.Accuracy <= 0 || location.Accuracy <= MaxAcceptedAccuracyMeters;

        return hasValidCoordinates && hasAcceptableAccuracy;
    }

    private bool IsLocationOutlier(Location candidate)
    {
        if (UserLocation != null)
        {
            var jumpDistance = CalculateDistanceKm(
                UserLocation.Latitude,
                UserLocation.Longitude,
                candidate.Latitude,
                candidate.Longitude);

            if (jumpDistance > MaxAcceptedJumpKm)
            {
                return true;
            }
        }

        if (_allPOIs.Count > 0)
        {
            var nearestPoiDistance = _allPOIs
                .Select(p => CalculateDistanceKm(candidate.Latitude, candidate.Longitude, p.Latitude, p.Longitude))
                .DefaultIfEmpty(double.MaxValue)
                .Min();

            if (nearestPoiDistance > MaxAcceptedJumpKm)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyMapSpanForPoiCollection(List<POI> pois, bool forceAll = false)
    {
        if (pois.Count == 0)
            return;

        // Nếu đã có vị trí người dùng và có POI gần đó, giữ camera theo người dùng.
        if (!forceAll && UserLocation != null)
        {
            var hasNearbyPoi = pois.Any(p =>
                CalculateDistanceKm(UserLocation.Latitude, UserLocation.Longitude, p.Latitude, p.Longitude) <= 20);

            if (hasNearbyPoi)
                return;
        }

        var centerLat = pois.Average(p => p.Latitude);
        var centerLon = pois.Average(p => p.Longitude);

        var radiusKm = Math.Max(1.0,
            pois.Max(p => CalculateDistanceKm(centerLat, centerLon, p.Latitude, p.Longitude)) * 1.3);

        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(centerLat, centerLon),
            Distance.FromKilometers(radiusKm));
    }

    private void ApplyMapSpanForTourAndUser(List<POI> tourPois)
    {
        var points = tourPois
            .Select(p => new Location(p.Latitude, p.Longitude))
            .ToList();

        if (UserLocation != null)
        {
            points.Add(UserLocation);
        }

        if (points.Count == 0)
            return;

        var minLat = points.Min(p => p.Latitude);
        var maxLat = points.Max(p => p.Latitude);
        var minLon = points.Min(p => p.Longitude);
        var maxLon = points.Max(p => p.Longitude);

        var centerLat = (minLat + maxLat) / 2;
        var centerLon = (minLon + maxLon) / 2;

        var verticalKm = CalculateDistanceKm(minLat, centerLon, maxLat, centerLon);
        var horizontalKm = CalculateDistanceKm(centerLat, minLon, centerLat, maxLon);
        var radiusKm = Math.Max(0.8, Math.Max(verticalKm, horizontalKm) * 0.7 + 0.4);

        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(centerLat, centerLon),
            Distance.FromKilometers(radiusKm));
    }

    private void SetMonitoredPois(IEnumerable<POI> pois)
    {
        _geofenceService.ClearPOIs();
        _geofenceService.AddPOIs(pois);
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
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
