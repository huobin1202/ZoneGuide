using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private int totalHistoryCount;

    [ObservableProperty]
    private string totalHistoryCountText = "0";

    [ObservableProperty]
    private string totalDurationText = "0 phút";

    [ObservableProperty]
    private int? currentNarrationPoiId;

    [ObservableProperty]
    private bool isCurrentNarrationPlaying;

    [ObservableProperty]
    private bool isCurrentNarrationPaused;

    public ObservableCollection<HistoryDayGroup> HistoryGroups { get; } = new();

    public HistoryViewModel(
        IAnalyticsRepository analyticsRepository,
        IPOIRepository poiRepository,
        INarrationService narrationService,
        ISettingsService settingsService)
    {
        _analyticsRepository = analyticsRepository;
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _settingsService = settingsService;

        _narrationService.NarrationStarted += OnNarrationChanged;
        _narrationService.NarrationCompleted += OnNarrationChanged;
        _narrationService.NarrationStopped += OnNarrationChanged;
        SyncNarrationState();
    }

    public async Task InitializeAsync()
    {
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var histories = await _analyticsRepository.GetNarrationsByDateRangeAsync(
                DateTime.UtcNow.AddYears(-10),
                DateTime.UtcNow.AddDays(1));
            var allPois = await _poiRepository.GetAllAsync();
            var poiLookup = allPois.ToDictionary(p => p.Id);

            var groupedByPoi = histories
                .GroupBy(h => h.POIId)
                .Select(g =>
                {
                    var ordered = g.OrderByDescending(x => x.StartTime).ToList();
                    var latest = ordered.First();
                    var totalDuration = ordered.Sum(x => ResolveDurationSeconds(x));
                    var playCount = ordered.Count;
                    var poi = poiLookup.TryGetValue(latest.POIId, out var p) ? p : null;

                    return BuildHistoryItem(latest, poi, playCount, totalDuration);
                })
                .OrderByDescending(x => x.PlayedAt)
                .ToList();

            TotalHistoryCount = histories.Count;
            TotalHistoryCountText = $"{TotalHistoryCount} {AppLocalizer.Instance.Translate("history_places_count")}";
            TotalDurationText = FormatTotalDuration(histories.Sum(ResolveDurationSeconds));

            HistoryGroups.Clear();
            foreach (var group in groupedByPoi.GroupBy(x => x.GroupDate))
            {
                var title = FormatGroupTitle(group.Key);
                HistoryGroups.Add(new HistoryDayGroup(title, group.ToList()));
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
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task ReplayAsync(HistoryEntryViewModel? item)
    {
        if (item == null)
            return;

        var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == item.POIId;
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

        var poi = await _poiRepository.GetByIdAsync(item.POIId);
        if (poi == null)
        {
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("status_not_found"),
                "POI is no longer available.",
                AppLocalizer.Instance.Translate("alert_ok"));
            return;
        }

        var queueItem = new NarrationQueueItem
        {
            POI = poi,
            AudioPath = poi.AudioFilePath,
            AudioUrl = poi.AudioUrl,
            TTSText = poi.TTSScript ?? poi.FullDescription,
            Language = string.IsNullOrWhiteSpace(item.LastLanguageCode)
                ? _settingsService.Settings.PreferredLanguage
                : item.LastLanguageCode,
            Priority = poi.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };

        await _narrationService.PlayImmediatelyAsync(queueItem);
        SyncNarrationState();
    }

    [RelayCommand]
    private async Task ToggleStopResumeAsync(HistoryEntryViewModel? item)
    {
        if (item == null)
            return;

        var isCurrentPoi = _narrationService.CurrentItem?.POI.Id == item.POIId;
        if (!isCurrentPoi)
            return;

        if (_narrationService.IsPlaying)
        {
            await _narrationService.PauseAsync();
        }

        SyncNarrationState();
    }

    [RelayCommand]
    private async Task DeleteAsync(HistoryEntryViewModel? item)
    {
        if (item == null)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            AppLocalizer.Instance.Translate("history_delete"),
            string.Format(AppLocalizer.Instance.Translate("history_delete_confirm"), item.Title),
            AppLocalizer.Instance.Translate("alert_delete"),
            AppLocalizer.Instance.Translate("alert_cancel"));

        if (!confirm)
            return;

        var byPoi = await _analyticsRepository.GetNarrationsByPOIAsync(item.POIId);
        foreach (var history in byPoi)
        {
            await _analyticsRepository.DeleteNarrationAsync(history.Id);
        }

        await LoadHistoryAsync();
    }

    private void OnNarrationChanged(object? sender, NarrationQueueItem e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            SyncNarrationState();

            // Chỉ reload khi kết thúc/dừng để tránh reload liên tục trong khi đang phát.
            if (_narrationService.IsPlaying)
                return;

            await LoadHistoryAsync();
        });
    }

    private void SyncNarrationState()
    {
        CurrentNarrationPoiId = _narrationService.CurrentItem?.POI.Id;
        IsCurrentNarrationPlaying = CurrentNarrationPoiId.HasValue && _narrationService.IsPlaying;
        IsCurrentNarrationPaused = CurrentNarrationPoiId.HasValue && _narrationService.IsPaused;
    }

    private static HistoryEntryViewModel BuildHistoryItem(NarrationHistory history, POI? poi, int playCount, int totalDuration)
    {
        var localTime = history.StartTime.ToLocalTime();
        var durationSeconds = totalDuration;

        return new HistoryEntryViewModel
        {
            Id = history.Id,
            POIId = history.POIId,
            Title = !string.IsNullOrWhiteSpace(history.POIName) ? history.POIName : (poi?.Name ?? string.Empty),
            Description = poi?.TTSScript ?? poi?.FullDescription ?? poi?.ShortDescription ?? AppLocalizer.Instance.Translate("history_subtitle"),
            Category = poi?.Category ?? AppLocalizer.Instance.Translate("category_other"),
            ImageUrl = POIListViewModel.ResolveImageSource(poi?.ImageUrl),
            PlayedAt = localTime,
            PlayedAtText = FormatRelativeTime(localTime),
            DurationSeconds = durationSeconds,
            DurationText = FormatDuration(durationSeconds),
            PlayCount = playCount,
            PlayCountText = playCount <= 1 ? "1 lần nghe" : $"{playCount} lần nghe",
            LastLanguageCode = history.Language,
            LastLanguageText = AppLocalizer.Instance.TranslateLanguageName(history.Language)
        };
    }

    private static int ResolveDurationSeconds(NarrationHistory history)
    {
        if (history.DurationSeconds > 0)
            return history.DurationSeconds;

        if (history.EndTime.HasValue)
        {
            return Math.Max(1, (int)Math.Round((history.EndTime.Value - history.StartTime).TotalSeconds));
        }

        return 0;
    }

    private static string FormatGroupTitle(DateTime date)
    {
        var today = DateTime.Now.Date;
        if (date == today)
            return AppLocalizer.Instance.Translate("status_today");
        if (date == today.AddDays(-1))
            return AppLocalizer.Instance.Translate("status_yesterday");

        return date.ToString("dd/MM/yyyy");
    }

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var now = DateTime.Now;
        var span = now - dateTime;

        if (span.TotalMinutes < 1)
            return AppLocalizer.Instance.Translate("status_just_now");
        if (span.TotalHours < 1)
            return $"{Math.Max(1, (int)span.TotalMinutes)} {AppLocalizer.Instance.Translate("status_minutes_ago")}";
        if (span.TotalDays < 1)
            return $"{Math.Max(1, (int)span.TotalHours)} {AppLocalizer.Instance.Translate("status_hours_ago")}";
        if (span.TotalDays < 2)
            return AppLocalizer.Instance.Translate("status_yesterday");

        return dateTime.ToString("dd/MM/yyyy HH:mm");
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
            return $"0 {AppLocalizer.Instance.Translate("duration_minute")}";

        var duration = TimeSpan.FromSeconds(seconds);
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}{AppLocalizer.Instance.Translate("duration_hour_short")} {duration.Minutes}{AppLocalizer.Instance.Translate("duration_minute_short")}";
        if (duration.TotalMinutes >= 1)
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} {AppLocalizer.Instance.Translate("duration_minute")}";

        return $"{seconds}{AppLocalizer.Instance.Translate("duration_second_short")}";
    }

    private static string FormatTotalDuration(int seconds)
    {
        if (seconds <= 0)
            return $"0 {AppLocalizer.Instance.Translate("duration_minute")}";

        var duration = TimeSpan.FromSeconds(seconds);
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}{AppLocalizer.Instance.Translate("duration_hour_short")} {duration.Minutes}{AppLocalizer.Instance.Translate("duration_minute_short")}";

        return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} {AppLocalizer.Instance.Translate("duration_minute")}";
    }
}

public sealed class HistoryDayGroup : ObservableCollection<HistoryEntryViewModel>
{
    public string Title { get; }

    public HistoryDayGroup(string title, IEnumerable<HistoryEntryViewModel> items)
        : base(items)
    {
        Title = title;
    }
}

public partial class HistoryEntryViewModel : ObservableObject
{
    public int Id { get; set; }
    public int POIId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime PlayedAt { get; set; }
    public string PlayedAtText { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string DurationText { get; set; } = string.Empty;
    public int PlayCount { get; set; }
    public string PlayCountText { get; set; } = string.Empty;
    public string LastLanguageCode { get; set; } = string.Empty;
    public string LastLanguageText { get; set; } = string.Empty;

    public DateTime GroupDate => PlayedAt.Date;
}