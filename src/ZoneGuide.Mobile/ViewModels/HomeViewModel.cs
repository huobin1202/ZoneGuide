using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly ITourRepository _tourRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly INarrationService _narrationService;
    private readonly ISettingsService _settingsService;
    private readonly ISyncService _syncService;
    private bool _isInitialized;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string welcomeTitle = string.Empty;

    [ObservableProperty]
    private string welcomeSubtitle = string.Empty;

    public ObservableCollection<HomePoiCardViewModel> NearbyPois { get; } = new();
    public ObservableCollection<HomeTourCardViewModel> FeaturedTours { get; } = new();
    public ObservableCollection<HomeHistoryCardViewModel> ContinueListening { get; } = new();
    public ObservableCollection<HomeTourCardViewModel> OfflineTours { get; } = new();

    public HomeViewModel(
        IPOIRepository poiRepository,
        ITourRepository tourRepository,
        IAnalyticsRepository analyticsRepository,
        ILocationService locationService,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        ISettingsService settingsService,
        ISyncService syncService)
    {
        _poiRepository = poiRepository;
        _tourRepository = tourRepository;
        _analyticsRepository = analyticsRepository;
        _locationService = locationService;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _settingsService = settingsService;
        _syncService = syncService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadHomeAsync(syncFirst: true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadHomeAsync(syncFirst: true);
    }

    [RelayCommand]
    private Task OpenAllPoisAsync() => Shell.Current.GoToAsync("//map?openSearch=true");

    [RelayCommand]
    private Task OpenToursAsync() => Shell.Current.GoToAsync("//tours");

    [RelayCommand]
    private Task OpenHistoryAsync() => NavigateToMoreChildAsync(nameof(HistoryPage));

    [RelayCommand]
    private Task OpenOfflineAsync() => NavigateToMoreChildAsync(nameof(OfflinePage));

    [RelayCommand]
    private Task OpenPoiDetailAsync(HomePoiCardViewModel? item)
    {
        if (item == null)
            return Task.CompletedTask;

        return Shell.Current.GoToAsync($"POIDetailPage?id={item.Id}");
    }

    [RelayCommand]
    private async Task PlayPoiAsync(HomePoiCardViewModel? item)
    {
        if (item?.Source == null)
            return;

        try
        {
            var poi = item.Source;
            var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == poi.Id;

            if (isCurrentPoi && _narrationService.IsPaused)
            {
                await _narrationService.ResumeAsync();
                return;
            }

            if (isCurrentPoi && _narrationService.IsPlaying)
                return;

            _geofenceService.ResetCooldown(poi.Id);
            await _narrationService.PlayImmediatelyAsync(POIListViewModel.CreateQueueItem(poi));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeVM] PlayPoi error: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task OpenTourDetailAsync(HomeTourCardViewModel? item)
    {
        if (item == null)
            return Task.CompletedTask;

        return Shell.Current.GoToAsync($"{nameof(TourDetailPage)}?id={item.Id}");
    }

    [RelayCommand]
    private async Task ReplayHistoryAsync(HomeHistoryCardViewModel? item)
    {
        if (item?.Source == null)
            return;

        try
        {
            var poi = item.Source;
            var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == poi.Id;

            if (isCurrentPoi && _narrationService.IsPaused)
            {
                await _narrationService.ResumeAsync();
                return;
            }

            if (isCurrentPoi && _narrationService.IsPlaying)
                return;

            _geofenceService.ResetCooldown(poi.Id);
            var queueItem = POIListViewModel.CreateQueueItem(poi);
            queueItem.Language = string.IsNullOrWhiteSpace(item.LanguageCode)
                ? _settingsService.Settings.PreferredLanguage
                : item.LanguageCode;

            await _narrationService.PlayImmediatelyAsync(queueItem);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeVM] ReplayHistory error: {ex.Message}");
        }
    }

    private async Task LoadHomeAsync(bool syncFirst)
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            if (syncFirst)
            {
                try
                {
                    await _syncService.SyncFromServerAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeVM] Sync failed (non-fatal): {ex.Message}");
                }
            }

            var location = await _locationService.GetCurrentLocationAsync();
            var pois = await _poiRepository.GetActiveAsync();
            var tours = await _tourRepository.GetActiveAsync();
            var histories = await _analyticsRepository.GetNarrationsByDateRangeAsync(
                DateTime.UtcNow.AddYears(-1),
                DateTime.UtcNow.AddDays(1));

            BuildWelcome(location);
            BuildNearbyPois(pois, location);
            BuildFeaturedTours(tours);
            BuildContinueListening(histories, pois);
            await BuildOfflineToursAsync(tours);
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    private void BuildWelcome(LocationData? location)
    {
        WelcomeTitle = AppLocalizer.Instance.Translate("home_title", "Home");
        WelcomeSubtitle = location == null
            ? AppLocalizer.Instance.Translate("home_subtitle_no_location", "Discover places, tours, and offline content from one screen.")
            : AppLocalizer.Instance.Translate("home_subtitle_with_location", "Start with nearby places, then continue where you left off.");
    }

    private void BuildNearbyPois(IEnumerable<POI> pois, LocationData? location)
    {
        NearbyPois.Clear();

        var items = (location == null
                ? pois.OrderBy(p => p.Name)
                : pois.OrderBy(p => p.CalculateDistance(location.Latitude, location.Longitude)))
            .Take(6)
            .Select(poi => new HomePoiCardViewModel
            {
                Id = poi.Id,
                Name = poi.Name,
                Category = AppLocalizer.Instance.TranslateCategory(poi.Category),
                DistanceText = location == null
                    ? AppLocalizer.Instance.Translate("home_nearby_distance_unknown", "Ready to explore")
                    : FormatDistance(poi.CalculateDistance(location.Latitude, location.Longitude)),
                ImageSource = POIListViewModel.ResolveImageSource(poi.ImageUrl),
                Source = poi
            });

        foreach (var item in items)
        {
            NearbyPois.Add(item);
        }
    }

    private void BuildFeaturedTours(IEnumerable<Tour> tours)
    {
        FeaturedTours.Clear();

        foreach (var tour in tours
                     .OrderByDescending(t => t.POICount)
                     .ThenByDescending(t => t.EstimatedDurationMinutes)
                     .Take(6))
        {
            FeaturedTours.Add(new HomeTourCardViewModel
            {
                Id = tour.Id,
                Name = tour.Name,
                Summary = $"{tour.POICount} {AppLocalizer.Instance.Translate("tour_detail_points", "stops")}",
                MetaText = FormatTourMeta(tour),
                ImageSource = POIListViewModel.ResolveImageSource(tour.ThumbnailUrl),
                Source = tour
            });
        }
    }

    private void BuildContinueListening(IEnumerable<NarrationHistory> histories, IReadOnlyCollection<POI> pois)
    {
        ContinueListening.Clear();

        var poiLookup = pois.ToDictionary(p => p.Id);
        var latestByPoi = histories
            .OrderByDescending(h => h.StartTime)
            .GroupBy(h => h.POIId)
            .Select(g => g.First())
            .Take(6);

        foreach (var history in latestByPoi)
        {
            if (!poiLookup.TryGetValue(history.POIId, out var poi))
                continue;

            ContinueListening.Add(new HomeHistoryCardViewModel
            {
                POIId = poi.Id,
                Title = string.IsNullOrWhiteSpace(history.POIName) ? poi.Name : history.POIName,
                Subtitle = AppLocalizer.Instance.TranslateCategory(poi.Category),
                PlayedAtText = FormatRelativeTime(history.StartTime.ToLocalTime()),
                LanguageCode = history.Language,
                LanguageText = AppLocalizer.Instance.TranslateLanguageName(history.Language),
                ImageSource = POIListViewModel.ResolveImageSource(poi.ImageUrl),
                Source = poi
            });
        }
    }

    private async Task BuildOfflineToursAsync(IEnumerable<Tour> tours)
    {
        OfflineTours.Clear();

        foreach (var tour in tours.Take(12))
        {
            if (!await _syncService.IsTourOfflineAvailableAsync(tour.Id))
                continue;

            OfflineTours.Add(new HomeTourCardViewModel
            {
                Id = tour.Id,
                Name = tour.Name,
                Summary = AppLocalizer.Instance.Translate("home_offline_ready", "Ready offline"),
                MetaText = FormatTourMeta(tour),
                ImageSource = POIListViewModel.ResolveImageSource(tour.ThumbnailUrl),
                Source = tour
            });

            if (OfflineTours.Count >= 6)
                break;
        }
    }

    private static string FormatDistance(double meters)
    {
        if (meters >= 1000)
            return $"{meters / 1000:0.0} km";

        return $"{Math.Round(meters):0} m";
    }

    private static string FormatTourMeta(Tour tour)
    {
        var distance = tour.EstimatedDistanceMeters >= 1000
            ? $"{tour.EstimatedDistanceMeters / 1000:0.#} km"
            : $"{Math.Round(tour.EstimatedDistanceMeters):0} m";

        return $"{tour.EstimatedDurationMinutes}m • {distance}";
    }

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;

        if (span.TotalMinutes < 1)
            return AppLocalizer.Instance.Translate("status_just_now", "Just now");
        if (span.TotalHours < 1)
            return $"{Math.Max(1, (int)span.TotalMinutes)} {AppLocalizer.Instance.Translate("status_minutes_ago", "minutes ago")}";
        if (span.TotalDays < 1)
            return $"{Math.Max(1, (int)span.TotalHours)} {AppLocalizer.Instance.Translate("status_hours_ago", "hours ago")}";
        if (span.TotalDays < 2)
            return AppLocalizer.Instance.Translate("status_yesterday", "Yesterday");

        return dateTime.ToString("dd/MM/yyyy");
    }

    private static async Task NavigateToMoreChildAsync(string route)
    {
        await Shell.Current.GoToAsync("//more");
        await Shell.Current.GoToAsync(route);
    }
}

public sealed class HomePoiCardViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DistanceText { get; init; } = string.Empty;
    public string ImageSource { get; init; } = "location.svg";
    public POI? Source { get; init; }
}

public sealed class HomeTourCardViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string MetaText { get; init; } = string.Empty;
    public string ImageSource { get; init; } = "route.svg";
    public Tour? Source { get; init; }
}

public sealed class HomeHistoryCardViewModel
{
    public int POIId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string PlayedAtText { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string LanguageText { get; init; } = string.Empty;
    public string ImageSource { get; init; } = "location.svg";
    public POI? Source { get; init; }
}
