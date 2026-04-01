using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace ZoneGuide.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

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
        builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
        
        // Register Repositories
        builder.Services.AddSingleton<IPOIRepository, POIRepository>();
        builder.Services.AddSingleton<IPOITranslationRepository, POITranslationRepository>();
        builder.Services.AddSingleton<ITourRepository, TourRepository>();
        builder.Services.AddSingleton<IAnalyticsRepository, AnalyticsRepository>();

        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MapViewModel>();
        builder.Services.AddSingleton<POIListViewModel>();
        builder.Services.AddSingleton<TourListViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddTransient<LanguageSelectionViewModel>();
        builder.Services.AddTransient<POIDetailViewModel>();
        builder.Services.AddTransient<TourDetailViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        // Register Views
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MapPage>();
        builder.Services.AddSingleton<POIListPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<TourListPage>();
        builder.Services.AddTransient<LanguageSelectionPage>();
        builder.Services.AddTransient<POIDetailPage>();
        builder.Services.AddTransient<TourDetailPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<SettingsPage>();

        return builder.Build();
    }
}
