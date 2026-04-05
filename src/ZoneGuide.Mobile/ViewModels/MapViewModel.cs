using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
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
    private const int MaxRenderedRoutePoints = 320;
    private const double DefaultTriggerRadiusMeters = 60;
    private const double MinTriggerRadiusMeters = 20;
    private const double MaxActivationRadiusMeters = 5000;
    private static readonly TimeSpan AutoOpenPoiDetailCooldown = TimeSpan.FromSeconds(20);
    private static readonly HttpClient RouteHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly ITourRepository _tourRepository;
    private readonly ISyncService _syncService;

    private bool _startTourRequested;
    private int? _requestedTourId;
    private bool _isTourModeActive;
    private readonly SemaphoreSlim _geofencePlaybackGate = new(1, 1);
    private bool _hasReliableUserLocation;
    private readonly List<POI> _currentTourPois = new();
    private int? _lastInRangePoiId;
    private int? _replayBlockedPoiId;
    private int? _lastAutoOpenedPoiId;
    private DateTime _lastAutoOpenedAtUtc = DateTime.MinValue;
    private CancellationTokenSource? _routeBuildCts;

    [ObservableProperty]
    private Location? userLocation;

    [ObservableProperty]
    private MapSpan? mapSpan;

    [ObservableProperty]
    private POI? selectedPOI;

    [ObservableProperty]
    private string selectedPoiDistanceDisplay = string.Empty;

    [ObservableProperty]
    private bool isSelectedPoiNarrationActive;

    [ObservableProperty]
    private bool isSelectedPoiNarrationPaused;

    [ObservableProperty]
    private bool isSelectedPoiPlayerVisible;

    [ObservableProperty]
    private int? currentNarrationPoiId;

    [ObservableProperty]
    private bool isNarrationPlaying;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? searchQuery;

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private string activeTourName = string.Empty;

    [ObservableProperty]
    private int activeTourPoiCount;

    [ObservableProperty]
    private bool isTourPoiListVisible;

    public bool IsTourModeActive
    {
        get => _isTourModeActive;
        private set
        {
            if (_isTourModeActive == value)
                return;

            _isTourModeActive = value;
            OnPropertyChanged();
        }
    }

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
        _narrationService.NarrationStarted += OnNarrationStateChanged;
        _narrationService.NarrationCompleted += OnNarrationStateChanged;
        _narrationService.NarrationStopped += OnNarrationStateChanged;

        SelectedCategory = Categories.FirstOrDefault();

        UpdateSelectedPoiNarrationState();
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
            _hasReliableUserLocation = false;

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
                    await _locationService.StartTrackingAsync(GPSAccuracyLevel.High);
                }

                var location = await _locationService.GetCurrentLocationAsync();
                if (location != null && IsValidLocation(location))
                {
                    var candidate = new Location(location.Latitude, location.Longitude);

                    if (!IsLocationOutlier(candidate))
                    {
                        UserLocation = candidate;
                        _hasReliableUserLocation = true;
                        await _geofenceService.ProcessLocationUpdateAsync(location);
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
                    else if (_hasReliableUserLocation && UserLocation != null)
                    {
                        MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.8));
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
            var activePois = await _poiRepository.GetActiveAsync();
            var sourcePois = activePois.Count > 0 ? activePois : await _poiRepository.GetAllAsync();

            if (activePois.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[MapVM] Active POIs are empty, fallback to all POIs for map rendering.");
            }

            var pois = sourcePois
                .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180)
                .ToList();

            _allPOIs = pois;
            PopulatePins(_allPOIs);
            if (!IsTourModeActive)
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
                Address = poi.TTSScript ?? poi.FullDescription ?? poi.ShortDescription,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            };
            
            MapPins.Add(pin);
        }
    }

    private async Task ApplyStartTourRouteIfRequestedAsync(int tourId)
    {
        var tourPois = (await _poiRepository.GetByTourIdAsync(tourId))
            .Where(p => p.IsActive)
            .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180)
            .OrderBy(p => p.OrderInTour)
            .ToList();

        if (tourPois.Count == 0)
        {
            ClearTourRoute();
            _currentTourPois.Clear();
            IsTourModeActive = false;
            ActiveTourName = string.Empty;
            ActiveTourPoiCount = 0;
            IsTourPoiListVisible = false;
            _startTourRequested = false;
            _requestedTourId = null;
            return;
        }

        var tour = await _tourRepository.GetByIdAsync(tourId);

        _currentTourPois.Clear();
        _currentTourPois.AddRange(tourPois);
        _replayBlockedPoiId = null;

        ActiveTourName = !string.IsNullOrWhiteSpace(tour?.Name)
            ? tour!.Name
            : "Tour";
        ActiveTourPoiCount = tourPois.Count;

        PopulatePins(tourPois);
        SetMonitoredPois(tourPois);
        IsTourModeActive = true;
        IsTourPoiListVisible = true;

        await SetTourRouteAsync(tourPois);
        await TriggerGeofenceAtCurrentLocationAsync();

        var firstPoi = tourPois[0];
        SelectedPOI = firstPoi;
        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(firstPoi.Latitude, firstPoi.Longitude),
            Distance.FromKilometers(0.8));

        _startTourRequested = false;
        _requestedTourId = null;
    }

    private async Task SetTourRouteAsync(List<POI> tourPois)
    {
        var fallbackRoute = tourPois
            .Select(p => new Location(p.Latitude, p.Longitude))
            .ToList();

        ReplaceTourRoutePoints(fallbackRoute);

        _routeBuildCts?.Cancel();
        _routeBuildCts?.Dispose();
        _routeBuildCts = new CancellationTokenSource();

        _ = BuildAndApplyRoadRouteAsync(tourPois, _routeBuildCts.Token);

        await Task.CompletedTask;
    }

    private async Task BuildAndApplyRoadRouteAsync(List<POI> tourPois, CancellationToken cancellationToken)
    {
        try
        {
            var roadRoute = await BuildRoadRouteAsync(tourPois, cancellationToken);
            if (cancellationToken.IsCancellationRequested || roadRoute.Count < 2)
                return;

            var simplified = DownsampleRoutePoints(roadRoute, MaxRenderedRoutePoints);
            ReplaceTourRoutePoints(simplified);
        }
        catch (OperationCanceledException)
        {
            // Ignore route build cancellation when user switches tours quickly.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] BuildAndApplyRoadRouteAsync failed: {ex.Message}");
        }
    }

    private static async Task<List<Location>> BuildRoadRouteAsync(List<POI> tourPois, CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();

            var from = new Location(tourPois[i].Latitude, tourPois[i].Longitude);
            var to = new Location(tourPois[i + 1].Latitude, tourPois[i + 1].Longitude);

            var segment = await GetRoadSegmentAsync(from, to, cancellationToken);
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

    private static async Task<List<Location>> GetRoadSegmentAsync(Location from, Location to, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = string.Create(
                CultureInfo.InvariantCulture,
                $"https://router.project-osrm.org/route/v1/driving/{from.Longitude},{from.Latitude};{to.Longitude},{to.Latitude}?overview=full&geometries=geojson");

            using var response = await RouteHttpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<Location>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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
        catch (OperationCanceledException)
        {
            throw;
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

    private void ReplaceTourRoutePoints(IReadOnlyList<Location> routePoints)
    {
        void Apply()
        {
            TourRoutePoints.Clear();
            foreach (var point in routePoints)
            {
                TourRoutePoints.Add(point);
            }
        }

        if (MainThread.IsMainThread)
        {
            Apply();
            return;
        }

        MainThread.BeginInvokeOnMainThread(Apply);
    }

    private static List<Location> DownsampleRoutePoints(List<Location> points, int maxPoints)
    {
        if (points.Count <= maxPoints || maxPoints < 3)
            return points;

        var sampled = new List<Location>(maxPoints) { points[0] };
        var step = (points.Count - 1d) / (maxPoints - 1d);

        for (var i = 1; i < maxPoints - 1; i++)
        {
            var index = (int)Math.Round(i * step, MidpointRounding.AwayFromZero);
            index = Math.Clamp(index, 1, points.Count - 2);
            sampled.Add(points[index]);
        }

        sampled.Add(points[^1]);
        return sampled;
    }

    private void ClearTourRoute()
    {
        _routeBuildCts?.Cancel();
        _routeBuildCts?.Dispose();
        _routeBuildCts = null;
        TourRoutePoints.Clear();
    }

    [RelayCommand]
    private void PerformSearch()
    {
        ClearTourRoute();
        IsTourModeActive = false;
        ActiveTourName = string.Empty;
        ActiveTourPoiCount = 0;
        IsTourPoiListVisible = false;
        _currentTourPois.Clear();
        _lastInRangePoiId = null;
        _replayBlockedPoiId = null;
        _lastAutoOpenedPoiId = null;

        _ = _narrationService.StopAsync();
        SetMonitoredPois(_allPOIs);

        if (string.IsNullOrWhiteSpace(SearchQuery) && string.IsNullOrWhiteSpace(SelectedCategory))
        {
            PopulatePins(_allPOIs);
            return;
        }

        var query = SearchQuery?.ToLowerInvariant();
        var results = _allPOIs.Where(p => 
            (string.IsNullOrWhiteSpace(query) || p.Name.ToLowerInvariant().Contains(query) || 
            (p.TTSScript != null && p.TTSScript.ToLowerInvariant().Contains(query)) ||
            (p.FullDescription != null && p.FullDescription.ToLowerInvariant().Contains(query)) ||
            (p.ShortDescription != null && p.ShortDescription.ToLowerInvariant().Contains(query))) &&
            (string.IsNullOrWhiteSpace(SelectedCategory) || IsAllCategorySelection(SelectedCategory) || p.Category == SelectedCategory))
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
        if (!_locationService.IsTracking)
        {
            await _locationService.StartTrackingAsync(GPSAccuracyLevel.High);
        }

        var location = await _locationService.GetCurrentLocationAsync();
        if (location != null && IsValidLocation(location))
        {
            var candidate = new Location(location.Latitude, location.Longitude);

            if (!IsLocationOutlier(candidate))
            {
                UserLocation = candidate;
                _hasReliableUserLocation = true;
                await _geofenceService.ProcessLocationUpdateAsync(location);
                MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
                return;
            }

            ErrorMessage = "Vị trí hiện tại chưa chính xác. Vui lòng thử lại.";
            System.Diagnostics.Debug.WriteLine(
                $"[MapVM] Ignored center-on-user outlier: {location.Latitude},{location.Longitude} (acc={location.Accuracy:F0}m)");

            if (_allPOIs.Count > 0)
            {
                ApplyMapSpanForPoiCollection(_allPOIs);
            }
        }
        else if (UserLocation != null)
        {
            MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
        }
        else
        {
            ErrorMessage = "Không thể lấy vị trí hiện tại. Vui lòng kiểm tra quyền truy cập vị trí.";
            if (_allPOIs.Count > 0)
            {
                ApplyMapSpanForPoiCollection(_allPOIs);
            }
        }
    }

    [RelayCommand]
    private void SelectPOI(POI poi)
    {
        SelectedPOI = poi;

        var radius = IsTourModeActive
            ? Distance.FromKilometers(0.6)
            : Distance.FromMeters(200);

        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(poi.Latitude, poi.Longitude), 
            radius);
    }

    [RelayCommand]
    private async Task PlaySelectedPOI()
    {
        if (SelectedPOI == null)
            return;

        var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == SelectedPOI.Id;
        if (isCurrentPoi && _narrationService.IsPaused)
        {
            _replayBlockedPoiId = null;
            await _narrationService.ResumeAsync();
            UpdateSelectedPoiNarrationState();
            return;
        }

        if (isCurrentPoi && _narrationService.IsPlaying)
        {
            UpdateSelectedPoiNarrationState();
            return;
        }

        _replayBlockedPoiId = null;
        _geofenceService.ResetCooldown(SelectedPOI.Id);

        var item = BuildNarrationItem(SelectedPOI, GeofenceEventType.Enter, 0);

        await _narrationService.PlayImmediatelyAsync(item);
        UpdateSelectedPoiNarrationState();
    }

    [RelayCommand]
    private async Task StopNarrationAndBlockReplay()
    {
        if (SelectedPOI == null)
            return;

        var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == SelectedPOI.Id;
        if (!isCurrentPoi)
            return;

        if (_narrationService.IsPlaying)
        {
            await _narrationService.PauseAsync();
        }

        UpdateSelectedPoiNarrationState();
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
        IsTourModeActive = false;
        ActiveTourName = string.Empty;
        ActiveTourPoiCount = 0;
        IsTourPoiListVisible = false;
        _currentTourPois.Clear();
        _lastInRangePoiId = null;
        _replayBlockedPoiId = null;
        _lastAutoOpenedPoiId = null;

        _ = _narrationService.StopAsync();
        SetMonitoredPois(_allPOIs);
        PopulatePins(_allPOIs);

        if (_allPOIs.Count > 0)
        {
            ApplyMapSpanForPoiCollection(_allPOIs, forceAll: true);
        }
    }

    [RelayCommand]
    private void ShowTourGroupMarkers()
    {
        if (!IsTourModeActive || _currentTourPois.Count == 0)
        {
            ShowAllMarkers();
            return;
        }

        SelectedPOI = null;
        PopulatePins(_currentTourPois);
        SetMonitoredPois(_currentTourPois);
        ApplyMapSpanForTourAndUser(_currentTourPois);
    }

    [RelayCommand]
    private async Task StopTourAsync()
    {
        SelectedPOI = null;
        ClearTourRoute();
        _currentTourPois.Clear();
        IsTourModeActive = false;
        ActiveTourName = string.Empty;
        ActiveTourPoiCount = 0;
        IsTourPoiListVisible = false;
        _startTourRequested = false;
        _requestedTourId = null;
        _lastInRangePoiId = null;
        _replayBlockedPoiId = null;
        _lastAutoOpenedPoiId = null;

        await _narrationService.StopAsync();
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
            _hasReliableUserLocation = true;
        });

        _ = _geofenceService.ProcessLocationUpdateAsync(location);
        _ = EnsureNarrationByCurrentLocationAsync(location);
    }

    private async void OnGeofenceTriggered(object? sender, GeofenceEvent evt)
    {
        await _geofencePlaybackGate.WaitAsync();

        try
        {
            var autoPlayEnabled = IsTourModeActive;
            var currentItemPoiId = _narrationService.CurrentItem?.POI.Id;
            var hasActiveNarration = _narrationService.IsPlaying || _narrationService.IsPaused;

            if (evt.EventType == GeofenceEventType.Exit)
            {
                if (autoPlayEnabled && hasActiveNarration && currentItemPoiId == evt.POI.Id)
                {
                    await _narrationService.StopAsync();
                }

                if (_replayBlockedPoiId == evt.POI.Id)
                {
                    _replayBlockedPoiId = null;
                }

                if (_lastInRangePoiId == evt.POI.Id)
                {
                    _lastInRangePoiId = null;
                }

                if (_lastAutoOpenedPoiId == evt.POI.Id)
                {
                    _lastAutoOpenedPoiId = null;
                }

                return;
            }

            if (evt.EventType == GeofenceEventType.Approach || evt.EventType == GeofenceEventType.Enter)
            {
                if (_replayBlockedPoiId != evt.POI.Id)
                {
                    await TryAutoOpenPoiDetailAsync(evt.POI);
                }
            }

            if (!autoPlayEnabled)
            {
                if (evt.EventType == GeofenceEventType.Enter)
                {
                    _lastInRangePoiId = evt.POI.Id;
                }

                return;
            }

            if (evt.EventType != GeofenceEventType.Enter)
                return;

            if (_replayBlockedPoiId.HasValue)
            {
                if (_replayBlockedPoiId == evt.POI.Id)
                {
                    _lastInRangePoiId = evt.POI.Id;
                    return;
                }

                _replayBlockedPoiId = null;
            }

            if (hasActiveNarration && currentItemPoiId == evt.POI.Id)
            {
                _lastInRangePoiId = evt.POI.Id;
                return;
            }

            if (hasActiveNarration)
            {
                await _narrationService.StopAsync();
            }

            await _narrationService.PlayImmediatelyAsync(BuildNarrationItem(
                evt.POI,
                evt.EventType,
                evt.Distance));

            _lastInRangePoiId = evt.POI.Id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnGeofenceTriggered play error: {ex.Message}");
        }
        finally
        {
            _geofencePlaybackGate.Release();
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
        if (!_hasReliableUserLocation || UserLocation == null)
        {
            return false;
        }

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
        var normalized = pois
            .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180)
            .ToList();

        var activePois = normalized.Where(p => p.IsActive).ToList();
        var monitored = activePois.Count > 0 ? activePois : normalized;

        _geofenceService.ClearPOIs();
        _geofenceService.AddPOIs(monitored);
    }

    private async Task TriggerGeofenceAtCurrentLocationAsync()
    {
        var current = _locationService.CurrentLocation ?? await _locationService.GetCurrentLocationAsync();

        if (current == null || !IsValidLocation(current))
            return;

        await _geofenceService.ProcessLocationUpdateAsync(current);
        await EnsureNarrationByCurrentLocationAsync(current);
    }

    private async Task EnsureNarrationByCurrentLocationAsync(LocationData location)
    {
        await _geofencePlaybackGate.WaitAsync();

        try
        {
            var autoPlayEnabled = IsTourModeActive;
            var monitored = _geofenceService.MonitoredPOIs;
            if (monitored.Count == 0)
                return;

            if (_replayBlockedPoiId.HasValue)
            {
                var blockedPoi = monitored.FirstOrDefault(p => p.Id == _replayBlockedPoiId.Value);
                if (blockedPoi == null)
                {
                    _replayBlockedPoiId = null;
                }
                else
                {
                    var blockedDistance = location.DistanceTo(blockedPoi.Latitude, blockedPoi.Longitude);
                    var blockedTriggerRadius = GetEffectiveTriggerRadiusMeters(blockedPoi);

                    if (blockedDistance > blockedTriggerRadius)
                    {
                        _replayBlockedPoiId = null;
                    }
                }
            }

            var inRange = monitored
                .Select(p => new
                {
                    Poi = p,
                    Distance = location.DistanceTo(p.Latitude, p.Longitude),
                    TriggerRadius = GetEffectiveTriggerRadiusMeters(p)
                })
                .Where(x => x.Distance <= x.TriggerRadius)
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.Poi.Priority)
                .FirstOrDefault();

            var activeItem = _narrationService.CurrentItem;
            var hasActiveNarration = _narrationService.IsPlaying || _narrationService.IsPaused;

            if (inRange == null)
            {
                _lastInRangePoiId = null;
                _lastAutoOpenedPoiId = null;

                if (autoPlayEnabled && hasActiveNarration && activeItem != null)
                {
                    var currentDistance = location.DistanceTo(activeItem.POI.Latitude, activeItem.POI.Longitude);
                    var currentTriggerRadius = GetEffectiveTriggerRadiusMeters(activeItem.POI);

                    if (currentDistance > currentTriggerRadius)
                    {
                        await _narrationService.StopAsync();
                    }
                }

                return;
            }

            var candidatePoi = inRange.Poi;

            if (_replayBlockedPoiId.HasValue)
            {
                if (_replayBlockedPoiId == candidatePoi.Id)
                {
                    _lastInRangePoiId = candidatePoi.Id;
                    return;
                }

                _replayBlockedPoiId = null;
            }

            if (_lastInRangePoiId != candidatePoi.Id)
            {
                await TryAutoOpenPoiDetailAsync(candidatePoi);
            }

            if (!autoPlayEnabled)
            {
                _lastInRangePoiId = candidatePoi.Id;
                return;
            }

            if (hasActiveNarration && activeItem != null)
            {
                if (activeItem.POI.Id == candidatePoi.Id)
                {
                    _lastInRangePoiId = candidatePoi.Id;
                    return;
                }

                await _narrationService.StopAsync();
                hasActiveNarration = false;
            }

            if (!hasActiveNarration)
            {
                if (_lastInRangePoiId == candidatePoi.Id)
                {
                    return;
                }

                await _narrationService.PlayImmediatelyAsync(BuildNarrationItem(
                    candidatePoi,
                    GeofenceEventType.Enter,
                    inRange.Distance));
            }

            _lastInRangePoiId = candidatePoi.Id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] EnsureNarrationByCurrentLocationAsync error: {ex.Message}");
        }
        finally
        {
            _geofencePlaybackGate.Release();
        }
    }

    private async Task TryAutoOpenPoiDetailAsync(POI poi)
    {
        try
        {
            var currentRoute = Shell.Current.CurrentState?.Location?.ToString() ?? string.Empty;
            if (!currentRoute.Contains("map", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (_lastAutoOpenedPoiId == poi.Id && now - _lastAutoOpenedAtUtc < AutoOpenPoiDetailCooldown)
            {
                return;
            }

            _lastAutoOpenedPoiId = poi.Id;
            _lastAutoOpenedAtUtc = now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectedPOI = poi;
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] TryAutoOpenPoiDetailAsync error: {ex.Message}");
        }
    }

    private static double GetEffectiveTriggerRadiusMeters(POI poi)
    {
        var triggerRadius = poi.TriggerRadius > 0 ? poi.TriggerRadius : DefaultTriggerRadiusMeters;
        return Math.Clamp(triggerRadius, MinTriggerRadiusMeters, MaxActivationRadiusMeters);
    }

    private static NarrationQueueItem BuildNarrationItem(POI poi, GeofenceEventType triggerType, double triggerDistance)
    {
        return new NarrationQueueItem
        {
            POI = poi,
            AudioPath = poi.AudioFilePath,
            AudioUrl = poi.AudioUrl,
            TTSText = GetNarrationText(poi),
            Language = poi.Language,
            Priority = poi.Priority,
            TriggerType = triggerType,
            TriggerDistance = triggerDistance
        };
    }

    private static string GetNarrationText(POI poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.TTSScript))
            return poi.TTSScript;

        if (!string.IsNullOrWhiteSpace(poi.FullDescription))
            return poi.FullDescription;

        if (!string.IsNullOrWhiteSpace(poi.ShortDescription))
            return poi.ShortDescription;

        return poi.Name;
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

    partial void OnSearchQueryChanged(string? value)
    {
        PerformSearch();
    }

    partial void OnSelectedPOIChanged(POI? value)
    {
        UpdateSelectedPoiDistanceDisplay();
        UpdateSelectedPoiNarrationState();
    }

    partial void OnUserLocationChanged(Location? value)
    {
        UpdateSelectedPoiDistanceDisplay();
    }

    private void OnNarrationStateChanged(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(UpdateSelectedPoiNarrationState);
    }

    private void UpdateSelectedPoiNarrationState()
    {
        CurrentNarrationPoiId = _narrationService.CurrentItem?.POI.Id;
        IsNarrationPlaying = CurrentNarrationPoiId.HasValue && _narrationService.IsPlaying;

        var selectedPoiId = SelectedPOI?.Id;
        var currentPoiId = CurrentNarrationPoiId;
        var isCurrentSelected = selectedPoiId.HasValue && currentPoiId.HasValue && selectedPoiId.Value == currentPoiId.Value;

        IsSelectedPoiNarrationActive = isCurrentSelected && _narrationService.IsPlaying;
        IsSelectedPoiNarrationPaused = isCurrentSelected && _narrationService.IsPaused;
        IsSelectedPoiPlayerVisible = SelectedPOI != null && (IsSelectedPoiNarrationActive || IsSelectedPoiNarrationPaused);
    }

    private void UpdateSelectedPoiDistanceDisplay()
    {
        if (SelectedPOI == null)
        {
            SelectedPoiDistanceDisplay = string.Empty;
            return;
        }

        if (UserLocation == null)
        {
            SelectedPoiDistanceDisplay = "Đang định vị";
            return;
        }

        var km = CalculateDistanceKm(
            UserLocation.Latitude,
            UserLocation.Longitude,
            SelectedPOI.Latitude,
            SelectedPOI.Longitude);

        SelectedPoiDistanceDisplay = DistanceUnitService.FormatFromMeters(km * 1000d);
    }

    public POI? FindNearestPOI()
    {
        return _geofenceService.NearestPOI;
    }

    public double? GetDistanceToNearestPOI()
    {
        return _geofenceService.NearestPOIDistance;
    }

    private static bool IsAllCategorySelection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim().ToLowerInvariant();
        var localizedAll = AppLocalizer.Instance.Translate("pois_filter_all", "Tất cả")
            .Trim()
            .ToLowerInvariant();

        return normalized is "tất cả" or "tat ca" or "all" or "全部" or "すべて" or "모두" or "tous"
               || normalized == localizedAll;
    }
}
