using Microsoft.Extensions.DependencyInjection;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
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
        AppLinkDispatcher.UriReceived += OnAppLinkReceived;
        UpdateLocalizedTitles();

        // Register routes
        Routing.RegisterRoute(nameof(POIDetailPage), typeof(POIDetailPage));
        Routing.RegisterRoute(nameof(TourDetailPage), typeof(TourDetailPage));
        Routing.RegisterRoute(nameof(POIListPage), typeof(POIListPage));
        Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
        Routing.RegisterRoute(nameof(OfflinePage), typeof(OfflinePage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(QRScannerPage), typeof(QRScannerPage));

        _ = HandlePendingAppLinkAsync();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        var services = Handler?.MauiContext?.Services;
        if (services == null || _narrationPrefsApplied)
            return;

        _ = ApplySavedNarrationPreferencesAsync(services);
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

    private async Task ApplySavedNarrationPreferencesAsync(IServiceProvider services)
    {
        try
        {
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
        catch (ObjectDisposedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Bo qua ap dung cai dat TTS do scope da dispose: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Áp dụng cài đặt TTS: {ex.Message}");
        }
    }

    private void OnAppLinkReceived(object? sender, Uri uri)
    {
        _ = MainThread.InvokeOnMainThreadAsync(() => NavigateFromAppLinkAsync(uri));
    }

    private async Task HandlePendingAppLinkAsync()
    {
        var pendingUri = AppLinkDispatcher.ConsumePendingUri();
        if (pendingUri == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() => NavigateFromAppLinkAsync(pendingUri));
    }

    private async Task NavigateFromAppLinkAsync(Uri uri)
    {
        if (!TryParsePoiLink(uri, out var poiId, out var autoplay))
            return;

        await GoToAsync($"//map?poiId={poiId}&autoplay={autoplay.ToString().ToLowerInvariant()}");
    }

    private static bool TryParsePoiLink(Uri uri, out int poiId, out bool autoplay)
    {
        poiId = 0;
        autoplay = true;

        if (!string.Equals(uri.Scheme, "zoneguide", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "poi", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (!int.TryParse(path, out poiId))
            return false;

        if (uri.Query.StartsWith("?autoplay=false", StringComparison.OrdinalIgnoreCase))
        {
            autoplay = false;
        }

        return true;
    }
}
