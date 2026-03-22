using Microsoft.Extensions.DependencyInjection;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile;

public partial class AppShell : Shell
{
    private static bool _narrationPrefsApplied;

    public AppShell()
    {
        InitializeComponent();

        // Register routes
        Routing.RegisterRoute(nameof(POIDetailPage), typeof(POIDetailPage));
        Routing.RegisterRoute(nameof(TourDetailPage), typeof(TourDetailPage));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.MauiContext is null || _narrationPrefsApplied)
            return;

        _ = ApplySavedNarrationPreferencesAsync();
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
