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
    private bool autoPlayOnEnter = true;

    [ObservableProperty]
    private bool notifyOnApproach = true;

    [ObservableProperty]
    private bool isSyncing;

    [ObservableProperty]
    private DateTime? lastSyncTime;

    [ObservableProperty]
    private string lastSyncTimeLabel = string.Empty;

    [ObservableProperty]
    private string selectedVoice = string.Empty;

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
        AutoPlayOnEnter = settings.AutoPlayOnEnter;
        NotifyOnApproach = settings.NotifyOnApproach;
        SelectedVoice = settings.PreferredVoice;
        SelectedLanguageOption = AvailableLanguages.FirstOrDefault(x => x.Code == PreferredLanguage) ?? AvailableLanguages.FirstOrDefault();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);
        RefreshLocalizedOptions();
        RefreshLocalizedOptions();

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

        settings.PreferredLanguage = PreferredLanguage;
        settings.TTSSpeed = TtsSpeed;
        settings.Volume = Volume;
        settings.GPSAccuracy = GpsAccuracy;
        settings.AutoPlayOnEnter = AutoPlayOnEnter;
        settings.NotifyOnApproach = NotifyOnApproach;
        settings.PreferredVoice = SelectedVoice;

        await _settingsService.SaveAsync();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);

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
}
