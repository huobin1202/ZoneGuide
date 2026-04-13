using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

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
        await Shell.Current.GoToAsync($"{nameof(TourDetailPage)}?id={tour.Id}");
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
    private readonly INarrationService _narrationService;
    private readonly IAudioService _audioService;
    private readonly ITTSService _ttsService;
    private readonly GlobalMiniPlayerViewModel _miniPlayerViewModel;
    private readonly MapViewModel _mapViewModel;
    private readonly AppLocalizer _localizer = AppLocalizer.Instance;
    private bool _isTourAudioSessionActive;
    private bool _isTourTtsActive;

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

    [ObservableProperty]
    private bool isTourAudioPlaying;

    [ObservableProperty]
    private bool isTourAudioPaused;

    [ObservableProperty]
    private double tourAudioProgress;

    [ObservableProperty]
    private string tourAudioButtonText = string.Empty;

    [ObservableProperty]
    private string estimatedDurationText = string.Empty;

    public ObservableCollection<POI> POIs { get; } = new();

    public TourDetailViewModel(
        ITourRepository tourRepository,
        IPOIRepository poiRepository,
        ISyncService syncService,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        IAudioService audioService,
        ITTSService ttsService,
        GlobalMiniPlayerViewModel miniPlayerViewModel,
        MapViewModel mapViewModel)
    {
        _tourRepository = tourRepository;
        _poiRepository = poiRepository;
        _syncService = syncService;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _audioService = audioService;
        _ttsService = ttsService;
        _miniPlayerViewModel = miniPlayerViewModel;
        _mapViewModel = mapViewModel;

        _audioService.ProgressChanged += OnAudioProgressChanged;
        _audioService.PlaybackCompleted += OnAudioPlaybackCompleted;
        _ttsService.SpeakCompleted += OnTourTtsCompleted;
        _mapViewModel.PropertyChanged += OnMapViewModelPropertyChanged;

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

    partial void OnIsTourAudioPlayingChanged(bool value)
    {
        RefreshDisplayState();
    }

    partial void OnIsTourAudioPausedChanged(bool value)
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
        await _mapViewModel.ActivateTourAsync(TourId);
        RefreshDisplayState();
    }

    private void RefreshDisplayState()
    {
        PoiCountDisplay = POIs.Count > 0
            ? POIs.Count
            : Tour?.POICount ?? 0;

        EstimatedDurationText = Tour == null
            ? string.Empty
            : $"{Tour.EstimatedDurationMinutes} {AppLocalizer.Instance.Translate("duration_minute_short")}";

        DistanceDisplay = Tour == null
            ? DistanceUnitService.FormatFromMeters(0)
            : DistanceUnitService.FormatFromMeters(Tour.EstimatedDistanceMeters);

        HighlightsText = BuildHighlightsText();
        TourAudioButtonText = BuildTourAudioButtonText();

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

    private void OnMapViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.IsTourModeActive) ||
            e.PropertyName == nameof(MapViewModel.ActiveTourId))
        {
            MainThread.BeginInvokeOnMainThread(RefreshDisplayState);
        }
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

    private string BuildTourAudioButtonText()
    {
        if (IsTourAudioPlaying)
            return _localizer.Translate("tour_detail_pause_audio", "Pause");

        if (IsTourAudioPaused)
            return _localizer.Translate("tour_detail_resume_audio", "Resume");

        return _localizer.Translate("tour_detail_listen_audio", "Listen");
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
    private async Task ToggleTourAudioAsync()
    {
        if (Tour == null)
            return;

        if (IsTourAudioPlaying)
        {
            await PauseTourAudioAsync();
            return;
        }

        if (IsTourAudioPaused)
        {
            await ResumeTourAudioAsync();
            return;
        }

        await PlayTourAudioAsync();
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

    private async Task PlayTourAudioAsync()
    {
        if (Tour == null)
            return;

        try
        {
            await _narrationService.StopAsync();
            await _audioService.StopAsync();
            await _ttsService.StopAsync();

            _isTourAudioSessionActive = true;
            _isTourTtsActive = false;
            TourAudioProgress = 0;

            if (!string.IsNullOrWhiteSpace(Tour.AudioFilePath) && File.Exists(Tour.AudioFilePath))
            {
                await _audioService.PlayAsync(Tour.AudioFilePath);
            }
            else if (!string.IsNullOrWhiteSpace(Tour.AudioUrl))
            {
                await _audioService.PlayFromUrlAsync(Tour.AudioUrl);
            }
            else if (!string.IsNullOrWhiteSpace(Tour.Description))
            {
                _isTourTtsActive = true;
                await _ttsService.SpeakAsync(Tour.Description, Tour.Language);
            }
            else
            {
                _isTourAudioSessionActive = false;
                await Shell.Current.DisplayAlert(
                    _localizer.Translate("tour_detail_audio_unavailable_title", "Notice"),
                    _localizer.Translate("tour_detail_audio_unavailable_message", "This tour does not have audio content yet."),
                    _localizer.Translate("alert_ok", "OK"));
                return;
            }

            _miniPlayerViewModel.SetActiveTour(Tour);
            IsTourAudioPlaying = true;
            IsTourAudioPaused = false;
        }
        catch (Exception ex)
        {
            _isTourAudioSessionActive = false;
            _isTourTtsActive = false;
            IsTourAudioPlaying = false;
            IsTourAudioPaused = false;
            _miniPlayerViewModel.ClearTourContext();
            System.Diagnostics.Debug.WriteLine($"[TourDetailVM] PlayTourAudioAsync error: {ex.Message}");
            await Shell.Current.DisplayAlert(
                _localizer.Translate("tour_detail_audio_error_title", "Error"),
                _localizer.Translate("tour_detail_audio_error_message", "Unable to play tour audio."),
                _localizer.Translate("alert_ok", "OK"));
        }
    }

    private async Task PauseTourAudioAsync()
    {
        if (!_isTourAudioSessionActive)
            return;

        if (_isTourTtsActive)
        {
            await _ttsService.StopAsync();
            _isTourAudioSessionActive = false;
            _isTourTtsActive = false;
            IsTourAudioPlaying = false;
            IsTourAudioPaused = false;
            TourAudioProgress = 0;
            _miniPlayerViewModel.ClearTourContext();
            return;
        }

        await _audioService.PauseAsync();
        IsTourAudioPlaying = false;
        IsTourAudioPaused = true;
        _miniPlayerViewModel.RefreshState();
    }

    private async Task ResumeTourAudioAsync()
    {
        if (_isTourTtsActive)
        {
            await PlayTourAudioAsync();
            return;
        }

        try
        {
            await _audioService.ResumeAsync();
            _isTourAudioSessionActive = true;
            IsTourAudioPlaying = true;
            IsTourAudioPaused = false;
            _miniPlayerViewModel.SetActiveTour(Tour);
        }
        catch
        {
            await PlayTourAudioAsync();
        }
    }

    private void OnAudioProgressChanged(object? sender, double progress)
    {
        if (!_isTourAudioSessionActive || _isTourTtsActive)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TourAudioProgress = Math.Clamp(progress, 0, 1);
            _miniPlayerViewModel.RefreshState();
        });
    }

    private void OnAudioPlaybackCompleted(object? sender, EventArgs e)
    {
        if (!_isTourAudioSessionActive || _isTourTtsActive)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isTourAudioSessionActive = false;
            IsTourAudioPlaying = false;
            IsTourAudioPaused = false;
            TourAudioProgress = 1;
            _miniPlayerViewModel.ClearTourContext();
        });
    }

    private void OnTourTtsCompleted(object? sender, EventArgs e)
    {
        if (!_isTourAudioSessionActive || !_isTourTtsActive)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isTourAudioSessionActive = false;
            _isTourTtsActive = false;
            IsTourAudioPlaying = false;
            IsTourAudioPaused = false;
            TourAudioProgress = 1;
            _miniPlayerViewModel.ClearTourContext();
        });
    }
}
