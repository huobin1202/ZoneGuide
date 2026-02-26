using HeriStepAI.Mobile.Services;
using HeriStepAI.Mobile.ViewModels;
using HeriStepAI.Mobile.Views;
using HeriStepAI.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace HeriStepAI.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Maps - chỉ bật khi có API key thật
        try
        {
            builder.UseMauiMaps();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiProgram] Maps init failed: {ex.Message}");
        }

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Add Plugin.Maui.Audio
        builder.Services.AddSingleton(Plugin.Maui.Audio.AudioManager.Current);

        // Register Services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<ILocationService, LocationService>();
        builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
        builder.Services.AddSingleton<ITTSService, TTSService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<INarrationService, NarrationService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<ApiService>();
        
        // Register Repositories
        builder.Services.AddSingleton<IPOIRepository, POIRepository>();
        builder.Services.AddSingleton<ITourRepository, TourRepository>();
        builder.Services.AddSingleton<IAnalyticsRepository, AnalyticsRepository>();

        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MapViewModel>();
        builder.Services.AddSingleton<POIListViewModel>();
        builder.Services.AddSingleton<TourListViewModel>();
        builder.Services.AddTransient<POIDetailViewModel>();
        builder.Services.AddTransient<TourDetailViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        // Register Views
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MapPage>();
        builder.Services.AddSingleton<POIListPage>();
        builder.Services.AddSingleton<TourListPage>();
        builder.Services.AddTransient<POIDetailPage>();
        builder.Services.AddTransient<TourDetailPage>();
        builder.Services.AddSingleton<SettingsPage>();

        return builder.Build();
    }
}
