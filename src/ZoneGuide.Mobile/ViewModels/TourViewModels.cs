using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho danh sách Tour
/// </summary>
public partial class TourListViewModel : ObservableObject
{
    private readonly ITourRepository _tourRepository;
    private readonly ISyncService _syncService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    public ObservableCollection<Tour> Tours { get; } = new();

    public TourListViewModel(
        ITourRepository tourRepository,
        ISyncService syncService)
    {
        _tourRepository = tourRepository;
        _syncService = syncService;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _syncService.SyncFromServerAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourListVM] Server sync failed (non-fatal): {ex.Message}");
        }

        await LoadToursAsync();
    }

    [RelayCommand]
    private async Task LoadToursAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var tours = await _tourRepository.GetActiveAsync();

            Tours.Clear();
            foreach (var tour in tours)
            {
                Tours.Add(tour);
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
        try
        {
            await _syncService.SyncFromServerAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourListVM] Server sync failed on refresh (non-fatal): {ex.Message}");
        }

        IsRefreshing = true;
        await LoadToursAsync();
    }

    [RelayCommand]
    private async Task ViewDetail(Tour tour)
    {
        await Shell.Current.GoToAsync($"TourDetailPage?id={tour.Id}");
    }
}

/// <summary>
/// ViewModel chi tiết Tour
/// </summary>
[QueryProperty(nameof(TourId), "id")]
public partial class TourDetailViewModel : ObservableObject
{
    private readonly ITourRepository _tourRepository;
    private readonly IPOIRepository _poiRepository;
    private readonly ISyncService _syncService;
    private readonly IGeofenceService _geofenceService;
    private readonly AppLocalizer _localizer = AppLocalizer.Instance;

    [ObservableProperty]
    private int tourId;

    [ObservableProperty]
    private Tour? tour;

    [ObservableProperty]
    private bool isOfflineAvailable;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private string offlineStatusText = string.Empty;

    [ObservableProperty]
    private string offlineActionText = string.Empty;

    [ObservableProperty]
    private Color offlineCardBackground = Colors.Gray;

    [ObservableProperty]
    private string distanceDisplay = "0 km";

    [ObservableProperty]
    private string highlightsText = string.Empty;

    [ObservableProperty]
    private int poiCountDisplay;

    public ObservableCollection<POI> POIs { get; } = new();

    public TourDetailViewModel(
        ITourRepository tourRepository,
        IPOIRepository poiRepository,
        ISyncService syncService,
        IGeofenceService geofenceService)
    {
        _tourRepository = tourRepository;
        _poiRepository = poiRepository;
        _syncService = syncService;
        _geofenceService = geofenceService;
        RefreshDisplayState();
    }

    async partial void OnTourIdChanged(int value)
    {
        await LoadTourAsync();
    }

    partial void OnIsOfflineAvailableChanged(bool value)
    {
        RefreshDisplayState();
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        RefreshDisplayState();
    }

    partial void OnTourChanged(Tour? value)
    {
        RefreshDisplayState();
    }

    private async Task LoadTourAsync()
    {
        Tour = await _tourRepository.GetByIdAsync(TourId);

        if (Tour != null)
        {
            IsOfflineAvailable = await _syncService.IsTourOfflineAvailableAsync(TourId);

            var pois = await _poiRepository.GetByTourIdAsync(TourId);

            if (Tour.POICount > 1 && pois.Count < Tour.POICount)
            {
                try
                {
                    await _syncService.SyncFromServerAsync();
                    pois = await _poiRepository.GetByTourIdAsync(TourId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TourDetailVM] Refresh POIs for tour {TourId} failed: {ex.Message}");
                }
            }

            POIs.Clear();
            foreach (var poi in pois)
            {
                POIs.Add(poi);
            }

            PoiCountDisplay = POIs.Count > 0 ? POIs.Count : Tour.POICount;
        }
        else
        {
            POIs.Clear();
            PoiCountDisplay = 0;
        }

        RefreshDisplayState();
    }

    private void RefreshDisplayState()
    {
        PoiCountDisplay = POIs.Count > 0
            ? POIs.Count
            : Tour?.POICount ?? 0;

        DistanceDisplay = Tour == null
            ? DistanceUnitService.FormatAsKilometers(0)
            : DistanceUnitService.FormatAsKilometers(Tour.EstimatedDistanceMeters);

        HighlightsText = BuildHighlightsText();

        if (IsDownloading)
        {
            OfflineCardBackground = Color.FromArgb("#374151");
            OfflineStatusText = _localizer.Translate("tour_detail_downloading", "Downloading content for offline use");
            OfflineActionText = _localizer.Translate("tour_detail_downloading_short", "Downloading");
            return;
        }

        if (IsOfflineAvailable)
        {
            OfflineCardBackground = Color.FromArgb("#111827");
            OfflineStatusText = _localizer.Translate("tour_detail_offline_ready", "Saved on this device and ready without internet");
            OfflineActionText = _localizer.Translate("tour_detail_remove_offline", "Remove");
            return;
        }

        OfflineCardBackground = Color.FromArgb("#111827");
        OfflineStatusText = _localizer.Translate("tour_detail_offline_prompt", "Download images and stops for use when connection is limited");
        OfflineActionText = _localizer.Translate("tour_detail_download_offline", "Download");
    }

    private string BuildHighlightsText()
    {
        if (POIs.Count == 0)
            return _localizer.Translate("tour_detail_highlights_empty", "Explore the route to discover featured stops.");

        var names = POIs
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Take(4)
            .Select(p => p.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();

        if (names.Count == 0)
            return _localizer.Translate("tour_detail_highlights_empty", "Explore the route to discover featured stops.");

        return string.Join(" • ", names);
    }

    [RelayCommand]
    private async Task StartTourAsync()
    {
        if (Tour == null || POIs.Count == 0)
            return;

        var activePois = POIs.Where(p => p.IsActive).ToList();
        if (activePois.Count == 0)
            return;

        _geofenceService.ClearPOIs();
        _geofenceService.AddPOIs(activePois);

        await Shell.Current.GoToAsync($"//map?tourId={Tour.Id}&startTour=true");
    }

    [RelayCommand]
    private async Task ToggleOfflineAsync()
    {
        if (IsOfflineAvailable)
            await DeleteOfflineAsync();
        else
            await DownloadOfflineAsync();
    }

    [RelayCommand]
    private async Task DownloadOfflineAsync()
    {
        if (IsDownloading)
            return;

        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            var success = await _syncService.DownloadTourOfflineAsync(TourId);
            IsOfflineAvailable = success;

            if (success)
            {
                await Shell.Current.DisplayAlert(
                    _localizer.Translate("tour_detail_download_success_title", "Success"),
                    _localizer.Translate("tour_detail_download_success_message", "Offline content downloaded successfully"),
                    _localizer.Translate("alert_ok", "OK"));
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    _localizer.Translate("tour_detail_download_error_title", "Error"),
                    _localizer.Translate("tour_detail_download_error_message", "Unable to download offline content"),
                    _localizer.Translate("alert_ok", "OK"));
            }
        }
        finally
        {
            IsDownloading = false;
            RefreshDisplayState();
        }
    }

    [RelayCommand]
    private async Task DeleteOfflineAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            _localizer.Translate("tour_detail_delete_confirm_title", "Confirm"),
            _localizer.Translate("tour_detail_delete_confirm_message", "Do you want to remove offline content for this tour?"),
            _localizer.Translate("alert_delete", "Delete"),
            _localizer.Translate("alert_cancel", "Cancel"));

        if (confirm)
        {
            var success = await _syncService.DeleteTourOfflineAsync(TourId);
            IsOfflineAvailable = !success;
            RefreshDisplayState();
        }
    }

    [RelayCommand]
    private async Task ViewPOIDetail(POI poi)
    {
        await Shell.Current.GoToAsync($"POIDetailPage?id={poi.Id}");
    }
}