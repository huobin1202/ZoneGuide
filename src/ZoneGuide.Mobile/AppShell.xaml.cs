using Microsoft.Extensions.DependencyInjection;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile;

public partial class AppShell : Shell
{
    private bool _narrationPrefsApplied;

    public AppShell()
    {
        InitializeComponent();

        AppLocalizer.Instance.PropertyChanged += OnLocalizerPropertyChanged;
        UpdateLocalizedTitles();

        // Register routes
        Routing.RegisterRoute(nameof(POIDetailPage), typeof(POIDetailPage));
        Routing.RegisterRoute(nameof(TourDetailPage), typeof(TourDetailPage));
        Routing.RegisterRoute(nameof(POIListPage), typeof(POIListPage));
        Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
        Routing.RegisterRoute(nameof(OfflinePage), typeof(OfflinePage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.MauiContext is null || _narrationPrefsApplied)
            return;

        _ = ApplySavedNarrationPreferencesAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppLocalizer.Instance.PropertyChanged -= OnLocalizerPropertyChanged;
    }

    private void OnLocalizerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
            return;

        MainThread.BeginInvokeOnMainThread(UpdateLocalizedTitles);
    }

    private void UpdateLocalizedTitles()
    {
        HomeTab.Title = AppLocalizer.Instance.Translate("tab_home");
        MapTab.Title = AppLocalizer.Instance.Translate("tab_map");
        TourTab.Title = AppLocalizer.Instance.Translate("tab_tours");
        MoreTab.Title = AppLocalizer.Instance.Translate("tab_more");
    }

    private async Task ApplySavedNarrationPreferencesAsync()
    {
        try
        {
            var services = Handler!.MauiContext!.Services;
            var settings = services.GetService<ISettingsService>();
            var narration = services.GetService<INarrationService>();
            if (settings == null || narration == null)
                return;

            await settings.LoadAsync();
            var s = settings.Settings;
            narration.SetVolume(s.Volume);
            narration.SetTTSSpeed(s.TTSSpeed);
            await narration.SetVoiceAsync(s.PreferredVoice);
            _narrationPrefsApplied = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Áp dụng cài đặt TTS: {ex.Message}");
        }
    }
}
