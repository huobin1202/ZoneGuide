using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.ObjectModel;

namespace ZoneGuide.Mobile.ViewModels;

/// <summary>
/// ViewModel cho trang Cài đặt
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private const double MinRadiusMeters = 10;
    private const double MaxRadiusMeters = 5000;

    private readonly ISettingsService _settingsService;
    private readonly ISyncService _syncService;
    private readonly ITTSService _ttsService;
    private readonly INarrationService _narrationService;

    [ObservableProperty]
    private string preferredLanguage = "vi-VN";

    [ObservableProperty]
    private float ttsSpeed = 1.0f;

    [ObservableProperty]
    private float volume = 1.0f;

    [ObservableProperty]
    private GPSAccuracyLevel gpsAccuracy = GPSAccuracyLevel.Medium;

    /// <summary>
    /// Index cho Picker GPS Accuracy: 0=Low, 1=Medium, 2=High
    /// </summary>
    [ObservableProperty]
    private int gpsAccuracyIndex = 1;

    [ObservableProperty]
    private double triggerRadius = 50;

    [ObservableProperty]
    private double approachRadius = 100;

    [ObservableProperty]
    private int cooldownSeconds = 300;

    [ObservableProperty]
    private bool autoPlayOnEnter = true;

    [ObservableProperty]
    private bool notifyOnApproach = true;

    [ObservableProperty]
    private bool batterySaverMode;

    [ObservableProperty]
    private bool backgroundTracking = true;

    [ObservableProperty]
    private bool offlineMode;

    [ObservableProperty]
    private bool autoDownloadOffline = true;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private DateTime? lastSyncTime;

    [ObservableProperty]
    private string lastSyncTimeLabel = string.Empty;

    [ObservableProperty]
    private string selectedVoice = string.Empty;

    [ObservableProperty]
    private bool useKilometerUnit;

    [ObservableProperty]
    private LanguageOptionItem? selectedLanguageOption;

    public ObservableCollection<LanguageOptionItem> AvailableLanguages { get; } = new();

    public ObservableCollection<string> AvailableVoices { get; } = new();
    public ObservableCollection<string> GpsAccuracyOptions { get; } = new();

    public SettingsViewModel(
        ISettingsService settingsService,
        ISyncService syncService,
        ITTSService ttsService,
        INarrationService narrationService)
    {
        _settingsService = settingsService;
        _syncService = syncService;
        _ttsService = ttsService;
        _narrationService = narrationService;

        foreach (var option in LanguageOptionItem.CreateDefaults())
        {
            AvailableLanguages.Add(option);
        }

        _syncService.SyncStarted += (s, e) => IsSyncing = true;
        _syncService.SyncCompleted += (s, e) => IsSyncing = false;

        RefreshLocalizedOptions();
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        var settings = _settingsService.Settings;

        PreferredLanguage = settings.PreferredLanguage;
        TtsSpeed = settings.TTSSpeed;
        Volume = settings.Volume;
        GpsAccuracy = settings.GPSAccuracy;
        GpsAccuracyIndex = settings.GPSAccuracy switch
        {
            GPSAccuracyLevel.Low => 0,
            GPSAccuracyLevel.High => 2,
            _ => 1
        };
        TriggerRadius = NormalizeRadius(settings.DefaultTriggerRadius);
        ApproachRadius = NormalizeApproachRadius(settings.DefaultApproachRadius, TriggerRadius);
        CooldownSeconds = settings.DefaultCooldownSeconds;
        AutoPlayOnEnter = settings.AutoPlayOnEnter;
        NotifyOnApproach = settings.NotifyOnApproach;
        BatterySaverMode = settings.BatterySaverMode;
        BackgroundTracking = settings.BackgroundTracking;
        OfflineMode = settings.OfflineMode;
        AutoDownloadOffline = settings.AutoDownloadOffline;
        SelectedVoice = settings.PreferredVoice;
    UseKilometerUnit = string.Equals(settings.DistanceUnit, "km", StringComparison.OrdinalIgnoreCase);
        SelectedLanguageOption = AvailableLanguages.FirstOrDefault(x => x.Code == PreferredLanguage) ?? AvailableLanguages.FirstOrDefault();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);
    DistanceUnitService.SetPreferredUnit(settings.DistanceUnit);

        LastSyncTime = _syncService.LastSyncTime;
        LastSyncTimeLabel = BuildLastSyncTimeLabel(LastSyncTime);

        _narrationService.SetVolume(settings.Volume);
        _narrationService.SetTTSSpeed(settings.TTSSpeed);
        await _narrationService.SetVoiceAsync(settings.PreferredVoice);

        await LoadVoicesAsync();
    }

    private void RefreshLocalizedOptions()
    {
        GpsAccuracyOptions.Clear();
        GpsAccuracyOptions.Add(AppLocalizer.Instance.Translate("settings_gps_low"));
        GpsAccuracyOptions.Add(AppLocalizer.Instance.Translate("settings_gps_medium"));
        GpsAccuracyOptions.Add(AppLocalizer.Instance.Translate("settings_gps_high"));
    }

    private static string BuildLastSyncTimeLabel(DateTime? value)
    {
        if (!value.HasValue)
            return string.Empty;

        return $"{AppLocalizer.Instance.Translate("settings_last_sync")}: {value.Value:dd/MM/yyyy HH:mm}";
    }

    private static double NormalizeRadius(double value)
    {
        return Math.Clamp(value, MinRadiusMeters, MaxRadiusMeters);
    }

    private static double NormalizeApproachRadius(double value, double triggerRadius)
    {
        var normalizedTrigger = NormalizeRadius(triggerRadius);
        var normalizedApproach = NormalizeRadius(value);
        return Math.Max(normalizedApproach, normalizedTrigger);
    }

    private async Task LoadVoicesAsync()
    {
        var voices = await _ttsService.GetVoicesAsync(PreferredLanguage);
        AvailableVoices.Clear();
        foreach (var voice in voices)
        {
            AvailableVoices.Add(voice);
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = _settingsService.Settings;
        var previousLanguage = settings.PreferredLanguage;
        var normalizedTriggerRadius = NormalizeRadius(TriggerRadius);
        var normalizedApproachRadius = NormalizeApproachRadius(ApproachRadius, normalizedTriggerRadius);

        TriggerRadius = normalizedTriggerRadius;
        ApproachRadius = normalizedApproachRadius;

        settings.PreferredLanguage = PreferredLanguage;
        settings.TTSSpeed = TtsSpeed;
        settings.Volume = Volume;
        settings.GPSAccuracy = GpsAccuracy;
        settings.DefaultTriggerRadius = normalizedTriggerRadius;
        settings.DefaultApproachRadius = normalizedApproachRadius;
        settings.DefaultCooldownSeconds = CooldownSeconds;
        settings.AutoPlayOnEnter = AutoPlayOnEnter;
        settings.NotifyOnApproach = NotifyOnApproach;
        settings.BatterySaverMode = BatterySaverMode;
        settings.BackgroundTracking = BackgroundTracking;
        settings.OfflineMode = OfflineMode;
        settings.AutoDownloadOffline = AutoDownloadOffline;
        settings.PreferredVoice = SelectedVoice;
    settings.DistanceUnit = UseKilometerUnit ? "km" : "m";

        await _settingsService.SaveAsync();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);
    DistanceUnitService.SetPreferredUnit(settings.DistanceUnit);

        var languageChanged = !string.Equals(previousLanguage, PreferredLanguage, StringComparison.OrdinalIgnoreCase);
        if (languageChanged)
        {
            try
            {
                await _syncService.SyncFromServerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Sync after language change failed: {ex.Message}");
            }
        }

        // Áp dụng settings
        _narrationService.SetVolume(Volume);
        _narrationService.SetTTSSpeed(TtsSpeed);

        await Shell.Current.DisplayAlert(
            AppLocalizer.Instance.Translate("settings_save_success_title"),
            AppLocalizer.Instance.Translate("settings_save_success_message"),
            AppLocalizer.Instance.Translate("alert_ok"));
    }

    [RelayCommand]
    private async Task SyncDataAsync()
    {
        if (IsSyncing)
            return;

        var success = await _syncService.SyncFromServerAsync();
        LastSyncTime = _syncService.LastSyncTime;
        LastSyncTimeLabel = BuildLastSyncTimeLabel(LastSyncTime);

        if (success)
        {
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("settings_save_success_title"),
                AppLocalizer.Instance.Translate("settings_sync_success_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
        else
        {
            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("settings_sync_error_title"),
                AppLocalizer.Instance.Translate("settings_sync_error_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
    }

    [RelayCommand]
    private async Task TestTTSAsync()
    {
        _ttsService.SetVolume(Volume);
        _ttsService.SetSpeed(TtsSpeed);
        _ttsService.SetVoice(SelectedVoice);

        var sample = PreferredLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "Hello! This is a sample voice from ZoneGuide."
            : "Xin chào! Đây là giọng đọc thử nghiệm từ ứng dụng ZoneGuide.";

        await _ttsService.SpeakAsync(sample, PreferredLanguage);
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            AppLocalizer.Instance.Translate("settings_clear_cache_confirm_title"),
            AppLocalizer.Instance.Translate("settings_clear_cache_confirm_message"),
            AppLocalizer.Instance.Translate("alert_delete"),
            AppLocalizer.Instance.Translate("alert_cancel"));

        if (confirm)
        {
            // Xóa thư mục offline
            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline");
            if (Directory.Exists(offlineDir))
            {
                Directory.Delete(offlineDir, true);
            }

            await Shell.Current.DisplayAlert(
                AppLocalizer.Instance.Translate("settings_save_success_title"),
                AppLocalizer.Instance.Translate("settings_clear_cache_success_message"),
                AppLocalizer.Instance.Translate("alert_ok"));
        }
    }

    partial void OnPreferredLanguageChanged(string value)
    {
        _ = LoadVoicesAsync();
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOptionItem? value)
    {
        if (value == null)
            return;

        PreferredLanguage = value.Code;
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);
        RefreshLocalizedOptions();
        LastSyncTimeLabel = BuildLastSyncTimeLabel(LastSyncTime);

        foreach (var option in AvailableLanguages)
        {
            option.IsSelected = ReferenceEquals(option, value);
        }
    }

    partial void OnGpsAccuracyIndexChanged(int value)
    {
        GpsAccuracy = value switch
        {
            0 => GPSAccuracyLevel.Low,
            2 => GPSAccuracyLevel.High,
            _ => GPSAccuracyLevel.Medium
        };
    }

    partial void OnTriggerRadiusChanged(double value)
    {
        var normalized = NormalizeRadius(value);
        if (Math.Abs(normalized - value) > 0.001)
        {
            TriggerRadius = normalized;
            return;
        }

        if (ApproachRadius < normalized)
        {
            ApproachRadius = normalized;
        }
    }

    partial void OnApproachRadiusChanged(double value)
    {
        var normalized = NormalizeApproachRadius(value, TriggerRadius);
        if (Math.Abs(normalized - value) > 0.001)
        {
            ApproachRadius = normalized;
        }
    }

    partial void OnVolumeChanged(float value)
    {
        _narrationService.SetVolume(value);
    }

    partial void OnTtsSpeedChanged(float value)
    {
        _narrationService.SetTTSSpeed(value);
    }

    partial void OnLastSyncTimeChanged(DateTime? value)
    {
        LastSyncTimeLabel = BuildLastSyncTimeLabel(value);
    }

    partial void OnUseKilometerUnitChanged(bool value)
    {
        DistanceUnitService.SetPreferredUnit(value ? "km" : "m");
    }
}
