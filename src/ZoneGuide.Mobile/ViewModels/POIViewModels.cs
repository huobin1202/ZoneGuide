using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho danh sách POI
/// </summary>
public partial class POIListViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
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
    private string? selectedCategory;

    [ObservableProperty]
    private int filteredCount;

    [ObservableProperty]
    private string filteredCountText = string.Empty;

    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty]
    private POI? selectedPOI;

    [ObservableProperty]
    private int? currentNarrationPoiId;

    [ObservableProperty]
    private bool isCurrentNarrationPlaying;

    [ObservableProperty]
    private bool isCurrentNarrationPaused;

    public ObservableCollection<POI> POIs { get; } = new();
    public ObservableCollection<POI> FilteredPOIs { get; } = new();

    public POIListViewModel(
        IPOIRepository poiRepository,
        IGeofenceService geofenceService,
        INarrationService narrationService,
        ISyncService syncService)
    {
        _poiRepository = poiRepository;
        _geofenceService = geofenceService;
        _narrationService = narrationService;
        _syncService = syncService;

        _narrationService.NarrationStarted += OnNarrationStateChanged;
        _narrationService.NarrationCompleted += OnNarrationStateChanged;
        _narrationService.NarrationStopped += OnNarrationStateChanged;

        RefreshLocalizedCategories();
        AppLocalizer.Instance.PropertyChanged += OnLocalizerPropertyChanged;
        SyncNarrationState();
    }

    public async Task InitializeAsync()
    {
        RefreshLocalizedCategories();
        FilteredCountText = $"{FilteredCount} {AppLocalizer.Instance.Translate("pois_count_suffix", "places")}";

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

            foreach (var poi in pois.OrderBy(p => p.Name))
            {
                POIs.Add(poi);
            }

            await SearchAsync();
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
            System.Diagnostics.Debug.WriteLine($"[POIListVM] Server sync failed on refresh (non-fatal): {ex.Message}");
        }

        IsRefreshing = true;
        await LoadPOIsAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        FilteredPOIs.Clear();

        IEnumerable<POI> results;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            results = POIs;
        }
        else
        {
            results = await _poiRepository.SearchAsync(SearchText);
        }

        if (!string.IsNullOrEmpty(SelectedCategory) && !IsCategoryFilterAll(SelectedCategory))
        {
            results = results.Where(p => IsCategoryMatch(p.Category, SelectedCategory));
        }

        foreach (var poi in results)
        {
            FilteredPOIs.Add(poi);
        }

        FilteredCount = FilteredPOIs.Count;
        FilteredCountText = $"{FilteredCount} {AppLocalizer.Instance.Translate("pois_count_suffix", "places")}";
    }

    [RelayCommand]
    private async Task ViewDetail(POI poi)
    {
        if (poi == null)
            return;

        try
        {
            await Shell.Current.GoToAsync($"POIDetailPage?id={poi.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIListVM] ViewDetail error: {ex}");
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("poi_detail_open_error_title"),
                AppLocalizer.Instance.Translate("poi_detail_open_error_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
    }

    [RelayCommand]
    private async Task PlayPOI(POI poi)
    {
        if (poi == null)
            return;

        try
        {
            var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == poi.Id;

            if (isCurrentPoi && _narrationService.IsPaused)
            {
                await _narrationService.ResumeAsync();
                SyncNarrationState();
                return;
            }

            if (isCurrentPoi && _narrationService.IsPlaying)
            {
                SyncNarrationState();
                return;
            }

            _geofenceService.ResetCooldown(poi.Id);

            var item = CreateQueueItem(poi);
            await _narrationService.PlayImmediatelyAsync(item);
            SyncNarrationState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIListVM] PlayPOI error: {ex}");
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("poi_detail_play_error_title"),
                AppLocalizer.Instance.Translate("poi_detail_play_error_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
    }

    [RelayCommand]
    private async Task ToggleStopResumePOI(POI poi)
    {
        if (poi == null)
            return;

        try
        {
            var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == poi.Id;
            if (!isCurrentPoi)
                return;

            if (_narrationService.IsPlaying)
            {
                await _narrationService.PauseAsync();
            }

            SyncNarrationState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIListVM] ToggleStopResumePOI error: {ex}");
        }
    }

    internal static NarrationQueueItem CreateQueueItem(POI poi)
    {
        return new NarrationQueueItem
        {
            POI = poi,
            AudioPath = poi.AudioFilePath,
            AudioUrl = poi.AudioUrl,
            TTSText = poi.TTSScript,
            Language = poi.Language,
            Priority = poi.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };
    }

    public static string ResolveImageSource(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return "location.svg";

        var trimmed = imageUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
                return absoluteUri.LocalPath;

            if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                return absoluteUri.ToString();

            return trimmed;
        }

        if (File.Exists(trimmed))
            return trimmed;

        var localPath = Path.Combine(FileSystem.AppDataDirectory, trimmed.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (File.Exists(localPath))
            return localPath;

        return trimmed;
    }

    public static string BuildPlaybackStatusText(bool isPlaying, bool isPaused, double progress)
    {
        if (isPaused)
            return AppLocalizer.Instance.Translate("poi_detail_ready");

        if (isPlaying)
            return $"{AppLocalizer.Instance.Translate("poi_detail_now_playing")} {Math.Round(progress * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";

        if (progress >= 1)
            return AppLocalizer.Instance.Translate("poi_detail_now_playing");

        return AppLocalizer.Instance.Translate("poi_detail_ready");
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        _ = SearchAsync();
    }

    private void OnLocalizerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var previousCategoryKey = string.IsNullOrWhiteSpace(SelectedCategory)
                ? "all"
                : NormalizeCategoryKey(SelectedCategory);

            RefreshLocalizedCategories();

            SelectedCategory = Categories
                .FirstOrDefault(c => NormalizeCategoryKey(c) == previousCategoryKey)
                ?? Categories.FirstOrDefault();

            _ = SearchAsync();
        });
    }

    private void RefreshLocalizedCategories()
    {
        var currentCategoryKey = string.IsNullOrWhiteSpace(SelectedCategory)
            ? "all"
            : NormalizeCategoryKey(SelectedCategory);

        Categories.Clear();
        Categories.Add(AppLocalizer.Instance.Translate("pois_filter_all", "All"));
        Categories.Add(AppLocalizer.Instance.Translate("category_tourism"));
        Categories.Add(AppLocalizer.Instance.Translate("category_service"));
        Categories.Add(AppLocalizer.Instance.Translate("category_food"));
        Categories.Add(AppLocalizer.Instance.Translate("category_entertainment"));
        Categories.Add(AppLocalizer.Instance.Translate("category_drinks"));
        Categories.Add(AppLocalizer.Instance.Translate("category_shopping"));

        SelectedCategory = Categories
            .FirstOrDefault(c => NormalizeCategoryKey(c) == currentCategoryKey)
            ?? Categories.FirstOrDefault();
    }

    private void OnNarrationStateChanged(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(SyncNarrationState);
    }

    private void SyncNarrationState()
    {
        CurrentNarrationPoiId = _narrationService.CurrentItem?.POI.Id;
        IsCurrentNarrationPlaying = CurrentNarrationPoiId.HasValue && _narrationService.IsPlaying;
        IsCurrentNarrationPaused = CurrentNarrationPoiId.HasValue && _narrationService.IsPaused;
    }

    private static bool IsCategoryFilterAll(string? category)
    {
        return NormalizeCategoryKey(category) == "all";
    }

    private static bool IsCategoryMatch(string? poiCategory, string? selectedCategory)
    {
        return NormalizeCategoryKey(poiCategory) == NormalizeCategoryKey(selectedCategory);
    }

    private static string NormalizeCategoryKey(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "other";

        var normalized = category.Trim().ToLowerInvariant();
        var localizedAll = AppLocalizer.Instance.Translate("pois_filter_all", "All").Trim().ToLowerInvariant();
        if (normalized == localizedAll)
            return "all";

        var localizedTourism = AppLocalizer.Instance.Translate("category_tourism").Trim().ToLowerInvariant();
        var localizedService = AppLocalizer.Instance.Translate("category_service").Trim().ToLowerInvariant();
        var localizedFood = AppLocalizer.Instance.Translate("category_food").Trim().ToLowerInvariant();
        var localizedEntertainment = AppLocalizer.Instance.Translate("category_entertainment").Trim().ToLowerInvariant();
        var localizedDrinks = AppLocalizer.Instance.Translate("category_drinks").Trim().ToLowerInvariant();
        var localizedShopping = AppLocalizer.Instance.Translate("category_shopping").Trim().ToLowerInvariant();

        if (normalized == localizedTourism) return "tourism";
        if (normalized == localizedService) return "service";
        if (normalized == localizedFood) return "food";
        if (normalized == localizedEntertainment) return "entertainment";
        if (normalized == localizedDrinks) return "drinks";
        if (normalized == localizedShopping) return "shopping";

        return normalized switch
        {
            "all" or "tất cả" => "all",
            "hải sản & ốc" or "hai san & oc" or "seafood & snails" or "seafood" => "tourism",
            "ăn vặt" or "an vat" or "snacks" or "snack" => "service",
            "lẩu & nướng" or "lau & nuong" or "hotpot & grill" or "hotpot" or "grill" => "food",
            "nhậu" or "nhau" or "drinking" or "pub" => "entertainment",
            "giải khát" or "giai khat" or "beverage" or "beverages" or "drinks" => "drinks",
            "ăn no" or "an no" or "hearty meals" or "main meal" => "shopping",

            // Backward compatibility for old category labels
            "tourism" or "du lịch" => "tourism",
            "service" or "services" or "dịch vụ" => "service",
            "food" or "food & drink" or "ăn uống" => "food",
            "entertainment" or "giải trí" => "entertainment",
            "shopping" or "mua sắm" or "other" or "khác" => "shopping",
            _ => category.Trim().ToLowerInvariant()
        };
    }
}

/// <summary>
/// ViewModel chi tiết POI / now playing
/// </summary>
[QueryProperty(nameof(POIIdString), "id")]
[QueryProperty(nameof(AutoPlayString), "autoplay")]
public partial class POIDetailViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly IGeofenceService _geofenceService;

    private string? _poiIdString;
    private bool _isHandlingNarrationState;

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

    private string? _autoPlayString;
    public string? AutoPlayString
    {
        get => _autoPlayString;
        set
        {
            if (SetProperty(ref _autoPlayString, value))
            {
                AutoPlay = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [ObservableProperty]
    private bool autoPlay;

    [ObservableProperty]
    private int poiId;

    [ObservableProperty]
    private POI? currentPoi;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private double? distance;

    [ObservableProperty]
    private string progressText = "0%";

    [ObservableProperty]
    private string playbackStatusText = AppLocalizer.Instance.Translate("poi_detail_ready");

    [ObservableProperty]
    private string imageSource = "location.svg";

    [ObservableProperty]
    private bool isPlayerPanelVisible;

    public POIDetailViewModel(
        IPOIRepository poiRepository,
        INarrationService narrationService,
        IGeofenceService geofenceService)
    {
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _geofenceService = geofenceService;

        _narrationService.NarrationStarted += OnNarrationStarted;
        _narrationService.NarrationCompleted += OnNarrationCompleted;
        _narrationService.NarrationStopped += OnNarrationStopped;
        _narrationService.ProgressUpdated += OnProgressUpdated;

        SyncFromNarrationService();
    }

    private async Task LoadPOIAsync()
    {
        CurrentPoi = await _poiRepository.GetByIdAsync(PoiId);
        ImageSource = POIListViewModel.ResolveImageSource(CurrentPoi?.ImageUrl);

        if (CurrentPoi != null && _geofenceService.NearestPOI?.Id == CurrentPoi.Id)
        {
            Distance = _geofenceService.NearestPOIDistance;
        }
        else
        {
            Distance = null;
        }

        SyncFromNarrationService();

        if (AutoPlay && CurrentPoi != null)
        {
            AutoPlay = false;
            if (_narrationService.CurrentItem?.POI.Id != CurrentPoi.Id || (!_narrationService.IsPlaying && !_narrationService.IsPaused))
            {
                await PlayAsync();
            }
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (CurrentPoi == null)
            return;

        try
        {
            IsPlayerPanelVisible = true;
            _geofenceService.ResetCooldown(CurrentPoi.Id);

            var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == CurrentPoi.Id;
            if (isCurrentPoi && _narrationService.IsPaused)
            {
                await _narrationService.ResumeAsync();
                SyncFromNarrationService();
                return;
            }

            await _narrationService.PlayImmediatelyAsync(POIListViewModel.CreateQueueItem(CurrentPoi));
            SyncFromNarrationService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetailVM] PlayAsync error: {ex}");
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("poi_detail_play_error_title"),
                AppLocalizer.Instance.Translate("poi_detail_play_error_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
    }

    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        if (CurrentPoi == null)
            return;

        IsPlayerPanelVisible = true;

        var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == CurrentPoi.Id;
        if (isCurrentPoi && _narrationService.IsPlaying)
        {
            await PauseAsync();
        }
        else
        {
            await PlayAsync();
        }
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        try
        {
            await _narrationService.PauseAsync();
            SyncFromNarrationService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetailVM] PauseAsync error: {ex}");
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            var isCurrentPoi = CurrentPoi != null && _narrationService.CurrentItem?.POI.Id == CurrentPoi.Id;
            if (!isCurrentPoi)
            {
                SyncFromNarrationService();
                return;
            }

            if (_narrationService.IsPlaying)
            {
                await _narrationService.PauseAsync();
            }

            SyncFromNarrationService();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIDetailVM] StopAsync error: {ex}");
        }
    }

    [RelayCommand]
    private async Task TogglePlayerPanelAsync()
    {
        IsPlayerPanelVisible = true;
        await TogglePlayPauseAsync();
    }

    [RelayCommand]
    private void HidePlayerPanel()
    {
        IsPlayerPanelVisible = false;
    }

    [RelayCommand]
    private async Task Rewind10SecondsAsync()
    {
        if (CurrentPoi == null)
            return;

        await _narrationService.RewindAsync(10);
        SyncFromNarrationService();
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await Shell.Current.GoToAsync("//pois");
    }

    [RelayCommand]
    private async Task NavigateAsync()
    {
        if (CurrentPoi == null)
            return;

        var action = await Shell.Current.DisplayActionSheet(
            "Chọn cách chỉ đường",
            "Hủy",
            null,
            "Chỉ đường trong app",
            "Google Maps");

        if (string.Equals(action, "Chỉ đường trong app", StringComparison.Ordinal))
        {
            await Shell.Current.GoToAsync($"//map?poiId={CurrentPoi.Id}&navigate=true");
            return;
        }

        if (string.Equals(action, "Google Maps", StringComparison.Ordinal))
        {
            var destination = $"{CurrentPoi.Latitude.ToString(CultureInfo.InvariantCulture)},{CurrentPoi.Longitude.ToString(CultureInfo.InvariantCulture)}";
            var url = $"https://www.google.com/maps/dir/?api=1&destination={destination}&travelmode=walking";
            await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(url);
        }
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (CurrentPoi == null)
            return;

        await Shell.Current.GoToAsync($"//map?poiId={CurrentPoi.Id}");
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (CurrentPoi == null)
            return;

        await Share.RequestAsync(new ShareTextRequest
        {
            Title = CurrentPoi.Name,
            Text = $"{CurrentPoi.Name}\n{CurrentPoi.TTSScript}\n{CurrentPoi.MapLink}"
        });
    }

    private void OnNarrationStarted(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPoi == null || item.POI.Id != CurrentPoi.Id)
                return;

            IsPlayerPanelVisible = true;
            IsPlaying = true;
            IsPaused = false;
            Progress = Math.Clamp(_narrationService.CurrentProgress, 0, 1);
            UpdateComputedPlaybackFields();
        });
    }

    private void OnNarrationCompleted(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPoi == null || item.POI.Id != CurrentPoi.Id)
                return;

            IsPlaying = false;
            IsPaused = false;
            Progress = 1;
            UpdateComputedPlaybackFields();
        });
    }

    private void OnNarrationStopped(object? sender, NarrationQueueItem item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPoi == null || item.POI.Id != CurrentPoi.Id)
                return;

            IsPlaying = false;
            IsPaused = false;
            Progress = 0;
            UpdateComputedPlaybackFields();
        });
    }

    private void OnProgressUpdated(object? sender, double value)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentPoi == null)
                return;

            if (_narrationService.CurrentItem?.POI.Id != CurrentPoi.Id)
                return;

            Progress = Math.Clamp(value, 0, 1);
            UpdateComputedPlaybackFields();
        });
    }

    private void SyncFromNarrationService()
    {
        if (_isHandlingNarrationState)
            return;

        _isHandlingNarrationState = true;

        try
        {
            var currentItem = _narrationService.CurrentItem;
            var isCurrentPoi = CurrentPoi != null && currentItem?.POI.Id == CurrentPoi.Id;

            IsPlaying = isCurrentPoi && _narrationService.IsPlaying;
            IsPaused = isCurrentPoi && _narrationService.IsPaused;
            Progress = isCurrentPoi ? Math.Clamp(_narrationService.CurrentProgress, 0, 1) : 0;
            UpdateComputedPlaybackFields();
        }
        finally
        {
            _isHandlingNarrationState = false;
        }
    }

    private void UpdateComputedPlaybackFields()
    {
        ProgressText = $"{Math.Round(Progress * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%";
        PlaybackStatusText = POIListViewModel.BuildPlaybackStatusText(IsPlaying, IsPaused, Progress);
    }
}
