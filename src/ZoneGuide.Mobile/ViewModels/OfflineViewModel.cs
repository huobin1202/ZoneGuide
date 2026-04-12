using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.Audio;
using System.Collections.ObjectModel;
using Android.Media;
using System.Text.Json;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

public partial class OfflineViewModel : ObservableObject
{
    private readonly IPOIRepository _poiRepository;
    private readonly IPOITranslationRepository _poiTranslationRepository;
    private readonly ApiService _apiService;
    private readonly ISyncService _syncService;
    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, RemoteAudioMetadata> _metadataCache = new(StringComparer.OrdinalIgnoreCase);
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
        IPOITranslationRepository poiTranslationRepository,
        ApiService apiService,
        ISyncService syncService,
        IAudioManager audioManager)
    {
        _poiRepository = poiRepository;
        _poiTranslationRepository = poiTranslationRepository;
        _apiService = apiService;
        _syncService = syncService;
        _audioManager = audioManager;
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

            var pois = await _poiRepository.GetActiveRawAsync();
            var items = new List<OfflinePackItemViewModel>();

            foreach (var poi in pois.OrderByDescending(BuildSortScore).ThenBy(p => p.Name))
            {
                var item = await BuildPackItemAsync(poi);
                if (item != null)
                {
                    items.Add(item);
                }
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

    private async Task<OfflinePackItemViewModel?> BuildPackItemAsync(POI poi)
    {
        var manifest = await ReadManifestAsync(poi.Id);
        var tracks = await BuildAudioTracksAsync(poi, manifest);
        if (tracks.Count == 0)
            return null;

        var estimatedTrackBytes = tracks
            .Where(t => t.SizeBytes.HasValue && t.SizeBytes.Value > 0)
            .Sum(t => t.SizeBytes!.Value);
        var totalBytes = manifest?.TotalBytes
            ?? (estimatedTrackBytes > 0 ? estimatedTrackBytes : await CalculatePackSizeAsync(poi.Id));
        var isDownloaded = await IsPackDownloadedAsync(poi.Id, tracks, manifest, poi);
        var totalDurationSeconds = tracks.Where(t => t.DurationSeconds.HasValue).Sum(t => t.DurationSeconds!.Value);

        var item = new OfflinePackItemViewModel
        {
            POIId = poi.Id,
            Title = poi.Name,
            CategoryText = _localizer.TranslateCategory(poi.Category),
            CategoryBackground = GetCategoryBackground(poi.Category),
            CategoryTextColor = GetCategoryForeground(poi.Category),
            ImageSource = POIListViewModel.ResolveImageSource(poi.ImagePath ?? poi.ImageUrl),
            TrackCountText = $"{tracks.Count} {_localizer.Translate("offline_audio_unit", "audio")}",
            DurationText = FormatDuration(totalDurationSeconds),
            SizeText = FormatBytes(totalBytes),
            DownloadedAtText = manifest != null ? manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy") : string.Empty,
            DownloadedStatusText = manifest != null
                ? string.Format(_localizer.Translate("offline_downloaded_at", "Downloaded {0}"), manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy"))
                : string.Empty,
            IsDownloaded = isDownloaded,
            IsExpanded = isDownloaded,
            TotalBytes = totalBytes
        };

        foreach (var track in tracks)
        {
            item.Tracks.Add(new OfflineTrackItemViewModel
            {
                Language = _localizer.TranslateLanguageName(track.LanguageCode),
                DurationText = FormatDuration(track.DurationSeconds),
                IsDownloaded = IsTrackDownloaded(poi.Id, track)
            });
        }

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
        var poi = await _poiRepository.GetByIdRawAsync(item.POIId);
        if (poi == null)
            return;

        var manifest = await ReadManifestAsync(item.POIId);
        var tracks = await BuildAudioTracksAsync(poi, manifest);
        var hasOfflineAssets = tracks.Count > 0 && await IsPackDownloadedAsync(item.POIId, tracks, manifest, poi);
        var estimatedTrackBytes = tracks
            .Where(t => t.SizeBytes.HasValue && t.SizeBytes.Value > 0)
            .Sum(t => t.SizeBytes!.Value);
        var totalBytes = manifest?.TotalBytes
            ?? (estimatedTrackBytes > 0 ? estimatedTrackBytes : await CalculatePackSizeAsync(item.POIId));
        var totalDurationSeconds = tracks.Where(t => t.DurationSeconds.HasValue).Sum(t => t.DurationSeconds!.Value);

        item.IsDownloaded = hasOfflineAssets;
        item.TotalBytes = totalBytes;
        item.TrackCountText = $"{tracks.Count} {_localizer.Translate("offline_audio_unit", "audio")}";
        item.DurationText = FormatDuration(totalDurationSeconds);
        item.SizeText = FormatBytes(totalBytes);
        item.DownloadedAtText = manifest != null ? manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy") : string.Empty;
        item.DownloadedStatusText = manifest != null
            ? string.Format(_localizer.Translate("offline_downloaded_at", "Downloaded {0}"), manifest.DownloadedAt.ToLocalTime().ToString("d/M/yyyy"))
            : string.Empty;

        item.Tracks.Clear();
        foreach (var track in tracks)
        {
            item.Tracks.Add(new OfflineTrackItemViewModel
            {
                Language = _localizer.TranslateLanguageName(track.LanguageCode),
                DurationText = FormatDuration(track.DurationSeconds),
                IsDownloaded = IsTrackDownloaded(item.POIId, track)
            });
        }

        if (!hasOfflineAssets)
            item.IsExpanded = false;

        RefreshSummary();
        ApplyFilter();
    }

    private async Task<bool> DownloadPackFilesAsync(int poiId)
    {
        var poi = await _poiRepository.GetByIdRawAsync(poiId);
        if (poi == null)
            return false;

        var tracks = await BuildAudioTracksAsync(poi, null);
        if (tracks.Count == 0)
            return false;

        var packDir = GetPackDirectory(poiId);
        Directory.CreateDirectory(packDir);

        long totalBytes = 0;
        var downloadedTracks = new List<OfflineAudioTrackManifest>();

        foreach (var track in tracks)
        {
            var audioData = await _apiService.DownloadAudioAsync(track.AudioUrl);
            if (audioData != null && audioData.Length > 0)
            {
                var audioPath = GetTrackAudioPath(poiId, track.LanguageCode);
                await File.WriteAllBytesAsync(audioPath, audioData);
                totalBytes += audioData.LongLength;
                var durationSeconds = GetAudioDurationSeconds(audioData);
                downloadedTracks.Add(new OfflineAudioTrackManifest
                {
                    LanguageCode = track.LanguageCode,
                    AudioUrl = track.AudioUrl,
                    FileName = Path.GetFileName(audioPath),
                    DurationSeconds = durationSeconds,
                    SizeBytes = audioData.LongLength
                });

                if (string.Equals(NormalizeLanguageCode(track.LanguageCode), NormalizeLanguageCode(poi.Language), StringComparison.OrdinalIgnoreCase))
                {
                    poi.AudioFilePath = audioPath;
                }
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
            }
        }

        if (downloadedTracks.Count == 0)
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
            TotalBytes = totalBytes,
            Tracks = downloadedTracks
        });

        await File.WriteAllTextAsync(GetManifestPath(poiId), manifestJson);
        return true;
    }

    private async Task DeletePackFilesAsync(int poiId)
    {
        var poi = await _poiRepository.GetByIdRawAsync(poiId);
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

    private static string GetTrackAudioPath(int poiId, string languageCode)
    {
        var safeLanguage = NormalizeLanguageCode(languageCode)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return Path.Combine(GetPackDirectory(poiId), $"audio_{safeLanguage}.mp3");
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

    private static string FormatDuration(int? totalSeconds)
    {
        if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
            return "--:--";

        var duration = TimeSpan.FromSeconds(totalSeconds.Value);
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    private async Task<List<OfflineAudioTrackCandidate>> BuildAudioTracksAsync(POI poi, OfflinePackManifest? manifest)
    {
        var result = new List<OfflineAudioTrackCandidate>();
        var seenLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task AddTrackAsync(
            string? languageCode,
            string? audioUrl,
            int? durationSeconds,
            long? sizeBytes,
            Func<int?, long?, Task>? persistMetadata)
        {
            if (string.IsNullOrWhiteSpace(audioUrl))
                return;

            var normalizedLanguage = NormalizeLanguageCode(languageCode);
            if (!seenLanguages.Add(normalizedLanguage))
                return;

            var manifestTrack = manifest?.Tracks
                .FirstOrDefault(t => string.Equals(NormalizeLanguageCode(t.LanguageCode), normalizedLanguage, StringComparison.OrdinalIgnoreCase));

            var resolvedDuration = manifestTrack?.DurationSeconds ?? durationSeconds;
            var resolvedSize = manifestTrack?.SizeBytes > 0
                ? manifestTrack.SizeBytes
                : sizeBytes;

            if ((!resolvedDuration.HasValue || !resolvedSize.HasValue || resolvedSize.Value <= 0)
                && !string.IsNullOrWhiteSpace(audioUrl))
            {
                var metadata = await TryGetRemoteAudioMetadataAsync(audioUrl);
                if (!resolvedDuration.HasValue)
                {
                    resolvedDuration = metadata.DurationSeconds;
                }

                if (!resolvedSize.HasValue || resolvedSize.Value <= 0)
                {
                    resolvedSize = metadata.SizeBytes;
                }

                if (persistMetadata != null && (resolvedDuration.HasValue || (resolvedSize.HasValue && resolvedSize.Value > 0)))
                {
                    await persistMetadata(resolvedDuration, resolvedSize);
                }
            }

            result.Add(new OfflineAudioTrackCandidate
            {
                LanguageCode = normalizedLanguage,
                AudioUrl = audioUrl,
                DurationSeconds = resolvedDuration,
                SizeBytes = resolvedSize
            });
        }

        await AddTrackAsync(
            poi.Language,
            poi.AudioUrl,
            poi.AudioDurationSeconds,
            poi.AudioFileSizeBytes,
            async (duration, size) =>
            {
                if (poi.AudioDurationSeconds == duration && poi.AudioFileSizeBytes == size)
                    return;

                poi.AudioDurationSeconds = duration;
                poi.AudioFileSizeBytes = size;
                await _poiRepository.UpdateAsync(poi);
            });

        var translations = await _poiTranslationRepository.GetByPOIIdAsync(poi.Id);
        foreach (var translation in translations)
        {
            await AddTrackAsync(
                translation.LanguageCode,
                translation.AudioUrl,
                translation.AudioDurationSeconds,
                translation.AudioFileSizeBytes,
                async (duration, size) =>
                {
                    if (translation.AudioDurationSeconds == duration && translation.AudioFileSizeBytes == size)
                        return;

                    translation.AudioDurationSeconds = duration;
                    translation.AudioFileSizeBytes = size;
                    await _poiTranslationRepository.UpdateAsync(translation);
                });
        }

        if (result.Count == 0)
        {
            var legacyAudioPath = poi.AudioFilePath;
            if (!string.IsNullOrWhiteSpace(legacyAudioPath) && File.Exists(legacyAudioPath))
            {
                result.Add(new OfflineAudioTrackCandidate
                {
                    LanguageCode = NormalizeLanguageCode(poi.Language),
                    AudioUrl = string.Empty,
                    DurationSeconds = GetAudioDurationSeconds(await File.ReadAllBytesAsync(legacyAudioPath)),
                    SizeBytes = new FileInfo(legacyAudioPath).Length
                });
            }
        }

        return result;
    }

    private async Task<bool> IsPackDownloadedAsync(int poiId, IReadOnlyCollection<OfflineAudioTrackCandidate> tracks, OfflinePackManifest? manifest, POI poi)
    {
        if (tracks.Count == 0)
            return false;

        foreach (var track in tracks)
        {
            if (!IsTrackDownloaded(poiId, track))
                return false;
        }

        if (manifest != null)
            return true;

        return !string.IsNullOrWhiteSpace(poi.AudioFilePath) && File.Exists(poi.AudioFilePath);
    }

    private static bool IsTrackDownloaded(int poiId, OfflineAudioTrackCandidate track)
    {
        var path = GetTrackAudioPath(poiId, track.LanguageCode);
        return File.Exists(path);
    }

    private int? GetAudioDurationSeconds(byte[] audioData)
    {
        try
        {
            using var stream = new MemoryStream(audioData, writable: false);
            using var player = _audioManager.CreatePlayer(stream);
            if (player.Duration <= 0)
                return null;

            return Math.Max(1, (int)Math.Round(player.Duration));
        }
        catch
        {
            return null;
        }
    }

    private async Task<RemoteAudioMetadata> TryGetRemoteAudioMetadataAsync(string audioUrl)
    {
        if (_metadataCache.TryGetValue(audioUrl, out var cached))
            return cached;

        var metadata = new RemoteAudioMetadata
        {
            SizeBytes = await TryGetRemoteFileSizeAsync(audioUrl),
            DurationSeconds = TryGetRemoteDurationSeconds(audioUrl)
        };

        _metadataCache[audioUrl] = metadata;
        return metadata;
    }

    private async Task<long?> TryGetRemoteFileSizeAsync(string url)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);
            if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                return headResponse.Content.Headers.ContentLength.Value;
        }
        catch
        {
        }

        try
        {
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
            using var getResponse = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
            if (getResponse.IsSuccessStatusCode && getResponse.Content.Headers.ContentLength.HasValue)
                return getResponse.Content.Headers.ContentLength.Value;
        }
        catch
        {
        }

        return null;
    }

    private static int? TryGetRemoteDurationSeconds(string url)
    {
        try
        {
            using var retriever = new MediaMetadataRetriever();
            retriever.SetDataSource(url, new Dictionary<string, string>());
            var durationMs = retriever.ExtractMetadata(MetadataKey.Duration);
            if (!long.TryParse(durationMs, out var durationInMs) || durationInMs <= 0)
                return null;

            return Math.Max(1, (int)Math.Round(durationInMs / 1000d));
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "vi-VN";

        var value = languageCode.Trim().Replace('_', '-');
        return value.ToLowerInvariant() switch
        {
            var c when c.StartsWith("vi") => "vi-VN",
            var c when c.StartsWith("en") => "en-US",
            var c when c.StartsWith("zh") => "zh-CN",
            var c when c.StartsWith("ja") => "ja-JP",
            var c when c.StartsWith("ko") => "ko-KR",
            var c when c.StartsWith("fr") => "fr-FR",
            _ => value
        };
    }

    private static Color GetCategoryBackground(string? category) =>
        NormalizeCategory(category) switch
        {
            "tourism" => Color.FromArgb("#FEE2E2"),
            "service" => Color.FromArgb("#DBEAFE"),
            "food" => Color.FromArgb("#FEF3C7"),
            "entertainment" => Color.FromArgb("#DCFCE7"),
            "drinks" => Color.FromArgb("#FFE4E6"),
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
            "drinks" => Color.FromArgb("#E11D48"),
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
            "giải khát" or "giai khat" or "beverage" or "beverages" or "drinks" => "drinks",
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
    public Color ActionButtonBackground => IsDownloaded ? Color.FromArgb("#FDECEC") : Color.FromArgb("#6D28D9");
    public Color ActionButtonTextColor => IsDownloaded ? Color.FromArgb("#EF4444") : Colors.White;
    public string RetryButtonText => "⟳";
    public string ExpandButtonText => IsExpanded ? "^" : "v";
    public Color BorderColor => IsDownloaded ? Color.FromArgb("#E9D5FF") : Color.FromArgb("#E5E7EB");
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
    public List<OfflineAudioTrackManifest> Tracks { get; set; } = new();
}

public sealed class OfflineAudioTrackManifest
{
    public string LanguageCode { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public long SizeBytes { get; set; }
}

internal sealed class OfflineAudioTrackCandidate
{
    public string LanguageCode { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public long? SizeBytes { get; set; }
}

internal sealed class RemoteAudioMetadata
{
    public int? DurationSeconds { get; set; }
    public long? SizeBytes { get; set; }
}
