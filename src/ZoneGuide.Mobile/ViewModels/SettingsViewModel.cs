using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
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
    private string selectedVoice = string.Empty;

    [ObservableProperty]
    private LanguageOptionItem? selectedLanguageOption;

    public ObservableCollection<LanguageOptionItem> AvailableLanguages { get; } = new();

    public ObservableCollection<string> AvailableVoices { get; } = new();

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
        TriggerRadius = settings.DefaultTriggerRadius;
        ApproachRadius = settings.DefaultApproachRadius;
        CooldownSeconds = settings.DefaultCooldownSeconds;
        AutoPlayOnEnter = settings.AutoPlayOnEnter;
        NotifyOnApproach = settings.NotifyOnApproach;
        BatterySaverMode = settings.BatterySaverMode;
        BackgroundTracking = settings.BackgroundTracking;
        OfflineMode = settings.OfflineMode;
        AutoDownloadOffline = settings.AutoDownloadOffline;
        SelectedVoice = settings.PreferredVoice;
        SelectedLanguageOption = AvailableLanguages.FirstOrDefault(x => x.Code == PreferredLanguage) ?? AvailableLanguages.FirstOrDefault();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);

        LastSyncTime = _syncService.LastSyncTime;

        _narrationService.SetVolume(settings.Volume);
        _narrationService.SetTTSSpeed(settings.TTSSpeed);
        await _narrationService.SetVoiceAsync(settings.PreferredVoice);

        await LoadVoicesAsync();
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

        settings.PreferredLanguage = PreferredLanguage;
        settings.TTSSpeed = TtsSpeed;
        settings.Volume = Volume;
        settings.GPSAccuracy = GpsAccuracy;
        settings.DefaultTriggerRadius = TriggerRadius;
        settings.DefaultApproachRadius = ApproachRadius;
        settings.DefaultCooldownSeconds = CooldownSeconds;
        settings.AutoPlayOnEnter = AutoPlayOnEnter;
        settings.NotifyOnApproach = NotifyOnApproach;
        settings.BatterySaverMode = BatterySaverMode;
        settings.BackgroundTracking = BackgroundTracking;
        settings.OfflineMode = OfflineMode;
        settings.AutoDownloadOffline = AutoDownloadOffline;
        settings.PreferredVoice = SelectedVoice;

        await _settingsService.SaveAsync();
        AppLocalizer.Instance.SetLanguage(PreferredLanguage);

        // Áp dụng settings
        _narrationService.SetVolume(Volume);
        _narrationService.SetTTSSpeed(TtsSpeed);

        await Shell.Current.DisplayAlert("Thành công", "Đã lưu cài đặt", "OK");
    }

    [RelayCommand]
    private async Task SyncDataAsync()
    {
        if (IsSyncing)
            return;

        var success = await _syncService.SyncFromServerAsync();
        LastSyncTime = _syncService.LastSyncTime;

        if (success)
        {
            await Shell.Current.DisplayAlert("Thành công", "Đã đồng bộ dữ liệu", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không thể đồng bộ dữ liệu", "OK");
        }
    }

    [RelayCommand]
    private async Task UploadAnalyticsAsync()
    {
        var success = await _syncService.UploadAnalyticsAsync();

        if (success)
        {
            await Shell.Current.DisplayAlert("Thành công", "Đã tải lên dữ liệu phân tích", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Lỗi", "Không thể tải lên dữ liệu", "OK");
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
            "Xác nhận",
            "Bạn có chắc muốn xóa tất cả dữ liệu cache?",
            "Xóa",
            "Hủy");

        if (confirm)
        {
            // Xóa thư mục offline
            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline");
            if (Directory.Exists(offlineDir))
            {
                Directory.Delete(offlineDir, true);
            }

            await Shell.Current.DisplayAlert("Thành công", "Đã xóa cache", "OK");
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
}
