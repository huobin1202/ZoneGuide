using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

public partial class OfflineViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly ApiService _apiService;
    private readonly ISyncService _syncService;
    private readonly AppLocalizer _localizer = AppLocalizer.Instance;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private OfflineFilterType selectedFilter = OfflineFilterType.All;

    [ObservableProperty]
    private string downloadedSummaryText = "0/0 pack";

    [ObservableProperty]
    private string storageSummaryText = "0 B";

    [ObservableProperty]
    private bool hasDownloadedItems;

    [ObservableProperty]
    private bool hasPendingItems;

    public ObservableCollection<OfflinePackItemViewModel> AllItems { get; } = new();
    public ObservableCollection<OfflinePackItemViewModel> FilteredItems { get; } = new();

    public bool IsAllSelected => SelectedFilter == OfflineFilterType.All;
    public bool IsDownloadedSelected => SelectedFilter == OfflineFilterType.Downloaded;
    public bool IsPendingSelected => SelectedFilter == OfflineFilterType.Pending;

    public OfflineViewModel(
        IPOIRepository poiRepository,
        ApiService apiService,
        ISyncService syncService)
    {
        _poiRepository = poiRepository;
        _apiService = apiService;
        _syncService = syncService;
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    partial void OnSelectedFilterChanged(OfflineFilterType value)
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsDownloadedSelected));
        OnPropertyChanged(nameof(IsPendingSelected));
        ApplyFilter();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            try
            {
                await _syncService.SyncFromServerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OfflineVM] Sync failed (non-fatal): {ex.Message}");
            }

            var pois = await _poiRepository.GetActiveAsync();
            var items = new List<OfflinePackItemViewModel>();

            foreach (var poi in pois.OrderByDescending(BuildSortScore).ThenBy(p => p.Name))
            {
                var item = await BuildPackItemAsync(poi);
                items.Add(item);
            }

            AllItems.Clear();
            foreach (var item in items)
            {
                AllItems.Add(item);
            }

            RefreshSummary();
            ApplyFilter();
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
        await LoadAsync();
    }

    [RelayCommand]
    private void ShowAll() => SelectedFilter = OfflineFilterType.All;

    [RelayCommand]
    private void ShowDownloaded() => SelectedFilter = OfflineFilterType.Downloaded;

    [RelayCommand]
    private void ShowPending() => SelectedFilter = OfflineFilterType.Pending;

    [RelayCommand]
    private void ToggleExpand(OfflinePackItemViewModel? item)
    {
        if (item != null)
            item.IsExpanded = !item.IsExpanded;
    }

    [RelayCommand]
    private async Task ToggleDownloadAsync(OfflinePackItemViewModel? item)
    {
        if (item == null || item.IsBusy)
            return;

        if (item.IsDownloaded)
            await DeletePackAsync(item);
        else
            await DownloadPackAsync(item);
    }

    [RelayCommand]
    private async Task RetryDownloadAsync(OfflinePackItemViewModel? item)
    {
        if (item != null && !item.IsBusy)
            await DownloadPackAsync(item);
    }

    [RelayCommand]
    private async Task DeletePackAsync(OfflinePackItemViewModel? item)
    {
        if (item == null || item.IsBusy)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            _localizer.Translate("offline_delete_title", "Delete offline pack"),
            string.Format(_localizer.Translate("offline_delete_message", "Do you want to remove '{0}' from this device?"), item.Title),
            _localizer.Translate("alert_delete", "Delete"),
            _localizer.Translate("alert_cancel", "Cancel"));

        if (!confirm)
            return;

        await SetBusyAsync(item, async () =>
        {
            await DeletePackFilesAsync(item.POIId);
            await UpdatePackStateAsync(item);
        });
    }

    [RelayCommand]
    private async Task DownloadAllAsync()
    {
        var pendingItems = AllItems.Where(i => !i.IsDownloaded).ToList();
        if (pendingItems.Count == 0)
            return;

        foreach (var item in pendingItems)
        {
            if (!item.IsBusy)
                await DownloadPackAsync(item, false);
        }

        await Shell.Current.DisplayAlert(
            _localizer.Translate("offline_download_done_title", "Done"),
            _localizer.Translate("offline_download_all_done_message", "Offline packs are ready."),
            _localizer.Translate("alert_ok", "OK"));
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (!HasDownloadedItems)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            _localizer.Translate("offline_clear_all_title", "Clear all"),
            _localizer.Translate("offline_clear_all_message", "Do you want to remove all downloaded offline packs?"),
            _localizer.Translate("alert_delete", "Delete"),
            _localizer.Translate("alert_cancel", "Cancel"));

        if (!confirm)
            return;

        foreach (var item in AllItems.Where(i => i.IsDownloaded).ToList())
        {
            await DeletePackFilesAsync(item.POIId);
            await UpdatePackStateAsync(item);
        }

        await Shell.Current.DisplayAlert(
            _localizer.Translate("offline_clear_all_title", "Clear all"),
            _localizer.Translate("offline_clear_all_done_message", "All offline packs have been removed."),
            _localizer.Translate("alert_ok", "OK"));
    }

    private async Task DownloadPackAsync(OfflinePackItemViewModel item, bool showResultAlert = true)
    {
        await SetBusyAsync(item, async () =>
        {
            var success = await DownloadPackFilesAsync(item.POIId);
            await UpdatePackStateAsync(item);

            if (!showResultAlert)
                return;

            await Shell.Current.DisplayAlert(
                success
                    ? _localizer.Translate("offline_download_done_title", "Done")
                    : _localizer.Translate("tour_detail_download_error_title", "Error"),
                success
                    ? string.Format(_localizer.Translate("offline_download_done_message", "Downloaded '{0}' for offline use."), item.Title)
                    : _localizer.Translate("tour_detail_download_error_message", "Unable to download offline content"),
                _localizer.Translate("alert_ok", "OK"));
        });
    }

    private async Task SetBusyAsync(OfflinePackItemViewModel item, Func<Task> action)
    {
        item.IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private async Task<OfflinePackItemViewModel> BuildPackItemAsync(POI poi)
    {
        var manifest = await ReadManifestAsync(poi.Id);
        var totalBytes = manifest?.TotalBytes ?? await CalculatePackSizeAsync(poi.Id);
        var isDownloaded = manifest != null || await HasOfflineAssetsAsync(poi.Id, poi);

        var item = new OfflinePackItemViewModel
        {
            POIId = poi.Id,
            Title = poi.Name,
            CategoryText = _localizer.TranslateCategory(poi.Category),
            CategoryBackground = GetCategoryBackground(poi.Category),
            CategoryTextColor = GetCategoryForeground(poi.Category),
            ImageSource = POIListViewModel.ResolveImageSource(poi.ImagePath ?? poi.ImageUrl),
            TrackCountText = $"1 {_localizer.Translate("offline_audio_unit", "audio")}",
            DurationText = EstimateDurationText(poi),
            SizeText = FormatBytes(totalBytes),
            DownloadedAtText = manifest != null ? manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy") : string.Empty,
            DownloadedStatusText = manifest != null
                ? string.Format(_localizer.Translate("offline_downloaded_at", "Downloaded {0}"), manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy"))
                : string.Empty,
            IsDownloaded = isDownloaded,
            IsExpanded = isDownloaded,
            TotalBytes = totalBytes
        };

        item.Tracks.Add(new OfflineTrackItemViewModel
        {
            Title = !string.IsNullOrWhiteSpace(poi.ShortDescription) ? poi.ShortDescription : poi.Name,
            Language = _localizer.TranslateLanguageName(poi.Language),
            DurationText = EstimateDurationText(poi),
            IsDownloaded = isDownloaded
        });

        return item;
    }

    private void RefreshSummary()
    {
        var downloadedCount = AllItems.Count(x => x.IsDownloaded);
        var totalCount = AllItems.Count;
        var totalBytes = AllItems.Where(x => x.IsDownloaded).Sum(x => x.TotalBytes);

        DownloadedSummaryText = $"{downloadedCount}/{totalCount} pack";
        StorageSummaryText = FormatBytes(totalBytes);
        HasDownloadedItems = downloadedCount > 0;
        HasPendingItems = downloadedCount < totalCount;
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();

        IEnumerable<OfflinePackItemViewModel> items = SelectedFilter switch
        {
            OfflineFilterType.Downloaded => AllItems.Where(x => x.IsDownloaded),
            OfflineFilterType.Pending => AllItems.Where(x => !x.IsDownloaded),
            _ => AllItems
        };

        foreach (var item in items)
            FilteredItems.Add(item);
    }

    private async Task UpdatePackStateAsync(OfflinePackItemViewModel item)
    {
        var poi = await _poiRepository.GetByIdAsync(item.POIId);
        if (poi == null)
            return;

        var manifest = await ReadManifestAsync(item.POIId);
        var hasOfflineAssets = manifest != null || await HasOfflineAssetsAsync(item.POIId, poi);
        var totalBytes = manifest?.TotalBytes ?? await CalculatePackSizeAsync(item.POIId);

        item.IsDownloaded = hasOfflineAssets;
        item.TotalBytes = totalBytes;
        item.SizeText = FormatBytes(totalBytes);
        item.DownloadedAtText = manifest != null ? manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy") : string.Empty;
        item.DownloadedStatusText = manifest != null
            ? string.Format(_localizer.Translate("offline_downloaded_at", "Downloaded {0}"), manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy"))
            : string.Empty;

        foreach (var track in item.Tracks)
            track.IsDownloaded = hasOfflineAssets;

        if (!hasOfflineAssets)
            item.IsExpanded = false;

        RefreshSummary();
        ApplyFilter();
    }

    private async Task<bool> DownloadPackFilesAsync(int poiId)
    {
        var poi = await _poiRepository.GetByIdAsync(poiId);
        if (poi == null)
            return false;

        var packDir = GetPackDirectory(poiId);
        Directory.CreateDirectory(packDir);

        long totalBytes = 0;
        var hasOfflineContent = false;

        if (!string.IsNullOrWhiteSpace(poi.AudioUrl))
        {
            var audioData = await _apiService.DownloadAudioAsync(poi.AudioUrl);
            if (audioData != null && audioData.Length > 0)
            {
                var audioPath = Path.Combine(packDir, $"audio_{poiId}.mp3");
                await File.WriteAllBytesAsync(audioPath, audioData);
                poi.AudioFilePath = audioPath;
                totalBytes += audioData.LongLength;
                hasOfflineContent = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(poi.ImageUrl))
        {
            var imageData = await _apiService.DownloadImageAsync(poi.ImageUrl);
            if (imageData != null && imageData.Length > 0)
            {
                var imagePath = Path.Combine(packDir, $"image_{poiId}.jpg");
                await File.WriteAllBytesAsync(imagePath, imageData);
                poi.ImagePath = imagePath;
                totalBytes += imageData.LongLength;
                hasOfflineContent = true;
            }
        }

        if (!hasOfflineContent && !string.IsNullOrWhiteSpace(poi.TTSScript))
            hasOfflineContent = true;

        if (!hasOfflineContent)
        {
            if (Directory.Exists(packDir) && !Directory.EnumerateFileSystemEntries(packDir).Any())
                Directory.Delete(packDir, false);

            return false;
        }

        await _poiRepository.UpdateAsync(poi);

        var manifestJson = JsonSerializer.Serialize(new OfflinePackManifest
        {
            POIId = poiId,
            DownloadedAt = DateTime.UtcNow,
            TotalBytes = totalBytes
        });

        await File.WriteAllTextAsync(GetManifestPath(poiId), manifestJson);
        return true;
    }

    private async Task DeletePackFilesAsync(int poiId)
    {
        var poi = await _poiRepository.GetByIdAsync(poiId);
        if (poi != null)
        {
            poi.AudioFilePath = null;

            var packDir = GetPackDirectory(poiId);
            if (!string.IsNullOrWhiteSpace(poi.ImagePath) &&
                poi.ImagePath.StartsWith(packDir, StringComparison.OrdinalIgnoreCase))
            {
                poi.ImagePath = null;
            }

            await _poiRepository.UpdateAsync(poi);
        }

        var directory = GetPackDirectory(poiId);
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    private Task<bool> HasOfflineAssetsAsync(int poiId, POI poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.AudioFilePath) && File.Exists(poi.AudioFilePath))
            return Task.FromResult(true);

        if (!string.IsNullOrWhiteSpace(poi.ImagePath) &&
            poi.ImagePath.StartsWith(GetPackDirectory(poiId), StringComparison.OrdinalIgnoreCase) &&
            File.Exists(poi.ImagePath))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(File.Exists(GetManifestPath(poiId)));
    }

    private Task<long> CalculatePackSizeAsync(int poiId)
    {
        var directory = GetPackDirectory(poiId);
        if (!Directory.Exists(directory))
            return Task.FromResult(0L);

        var total = Directory
            .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path).Length)
            .Sum();

        return Task.FromResult(total);
    }

    private async Task<OfflinePackManifest?> ReadManifestAsync(int poiId)
    {
        var manifestPath = GetManifestPath(poiId);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<OfflinePackManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string GetPackDirectory(int poiId) =>
        Path.Combine(FileSystem.AppDataDirectory, "offline", "packs", poiId.ToString());

    private static string GetManifestPath(int poiId) =>
        Path.Combine(GetPackDirectory(poiId), "manifest.json");

    private static string EstimateDurationText(POI poi)
    {
        var source = poi.TTSScript ?? poi.FullDescription ?? poi.ShortDescription ?? poi.Name;
        var wordCount = source
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var minutes = Math.Max(1, wordCount / 130.0);
        var duration = TimeSpan.FromMinutes(minutes);
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    private static int BuildSortScore(POI poi)
    {
        var baseScore = poi.Priority * 10;
        if (!string.IsNullOrWhiteSpace(poi.AudioFilePath))
            baseScore += 1000;

        return baseScore;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var index = 0;

        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }

        var format = index == 0 ? "0" : "0.#";
        return $"{size.ToString(format)} {units[index]}";
    }

    private static Color GetCategoryBackground(string? category) =>
        NormalizeCategory(category) switch
        {
            "tourism" => Color.FromArgb("#FEE2E2"),
            "service" => Color.FromArgb("#DBEAFE"),
            "food" => Color.FromArgb("#FEF3C7"),
            "entertainment" => Color.FromArgb("#DCFCE7"),
            "shopping" => Color.FromArgb("#EDE9FE"),
            _ => Color.FromArgb("#E0F2FE")
        };

    private static Color GetCategoryForeground(string? category) =>
        NormalizeCategory(category) switch
        {
            "tourism" => Color.FromArgb("#DC2626"),
            "service" => Color.FromArgb("#2563EB"),
            "food" => Color.FromArgb("#D97706"),
            "entertainment" => Color.FromArgb("#16A34A"),
            "shopping" => Color.FromArgb("#9333EA"),
            _ => Color.FromArgb("#0F766E")
        };

    private static string NormalizeCategory(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "hải sản & ốc" or "hai san & oc" or "seafood & snails" or "seafood" => "tourism",
            "ăn vặt" or "an vat" or "snacks" or "snack" => "service",
            "lẩu & nướng" or "lau & nuong" or "hotpot & grill" or "hotpot" or "grill" => "food",
            "nhậu" or "nhau" or "drinking" or "pub" => "entertainment",
            "ăn no" or "an no" or "hearty meals" or "main meal" => "shopping",

            // Backward compatibility for old category labels
            "du lịch" or "tourism" => "tourism",
            "dịch vụ" or "service" or "services" => "service",
            "ăn uống" or "food" or "food & drink" => "food",
            "giải trí" or "entertainment" => "entertainment",
            "mua sắm" or "shopping" or "khác" or "other" => "shopping",
            _ => "other"
        };
}

public enum OfflineFilterType
{
    All,
    Downloaded,
    Pending
}

public partial class OfflinePackItemViewModel : ObservableObject
{
    public int POIId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryText { get; set; } = string.Empty;
    public Color CategoryBackground { get; set; } = Colors.LightGray;
    public Color CategoryTextColor { get; set; } = Colors.Black;
    public string ImageSource { get; set; } = "location.svg";
    public string TrackCountText { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;

    [ObservableProperty]
    private string sizeText = "0 B";

    [ObservableProperty]
    private string downloadedAtText = string.Empty;

    [ObservableProperty]
    private string downloadedStatusText = string.Empty;

    [ObservableProperty]
    private bool isDownloaded;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<OfflineTrackItemViewModel> Tracks { get; } = new();
    public long TotalBytes { get; set; }
    public string ActionButtonText => IsDownloaded ? "🗑" : "↓";
    public Color ActionButtonBackground => IsDownloaded ? Color.FromArgb("#FDECEC") : Color.FromArgb("#16A34A");
    public Color ActionButtonTextColor => IsDownloaded ? Color.FromArgb("#EF4444") : Colors.White;
    public string RetryButtonText => "⟳";
    public string ExpandButtonText => IsExpanded ? "^" : "v";
    public Color BorderColor => IsDownloaded ? Color.FromArgb("#BBF7D0") : Color.FromArgb("#E5E7EB");
    public bool ShowDownloadedStatus => IsDownloaded && !string.IsNullOrWhiteSpace(DownloadedStatusText);
    public bool ShowRetryButton => IsDownloaded;

    partial void OnIsDownloadedChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(ActionButtonBackground));
        OnPropertyChanged(nameof(ActionButtonTextColor));
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(ShowDownloadedStatus));
        OnPropertyChanged(nameof(ShowRetryButton));
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandButtonText));

    partial void OnDownloadedStatusTextChanged(string value) => OnPropertyChanged(nameof(ShowDownloadedStatus));
}

public partial class OfflineTrackItemViewModel : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isDownloaded;

    public string TrackStatusIcon => IsDownloaded ? "○" : "◌";

    partial void OnIsDownloadedChanged(bool value) => OnPropertyChanged(nameof(TrackStatusIcon));
}

public sealed class OfflinePackManifest
{
    public int POIId { get; set; }
    public DateTime DownloadedAt { get; set; }
    public long TotalBytes { get; set; }
}
