using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settingsService;

    public App(IServiceProvider services, ISettingsService settingsService)
    {
        InitializeComponent();
        _services = services;
        _settingsService = settingsService;

        // Bắt unhandled exceptions để debug
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRASH] Unhandled: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[CRASH] Unobserved Task: {e.Exception}");
            e.SetObserved();
        };

        // Bắt exception trong MAUI
        this.PageAppearing += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[App] Page Appearing: {e.GetType().Name}");
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(CreateLoadingPage());

        window.Created += async (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("[App] Window Created successfully");
            await InitializeRootPageAsync(window);
        };

        return window;
    }

    private async Task InitializeRootPageAsync(Window window)
    {
        try
        {
            await _settingsService.LoadAsync();
            AppLocalizer.Instance.SetLanguage(_settingsService.Settings.PreferredLanguage);

            var appShell = _services.GetRequiredService<AppShell>();
            var languageSelectionPage = _services.GetRequiredService<LanguageSelectionPage>();

            var rootPage = _settingsService.Settings.HasCompletedLanguageSelection
                ? (Page)appShell
                : languageSelectionPage;

            await MainThread.InvokeOnMainThreadAsync(() => window.Page = rootPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Root initialization failed: {ex}");

            var fallbackPage = CreateFallbackErrorPage(ex);
            await MainThread.InvokeOnMainThreadAsync(() => window.Page = fallbackPage);
        }
    }

    private static Page CreateLoadingPage()
    {
        return new ContentPage
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true
                    },
                    new Label
                    {
                        Text = "Dang mo ung dung...",
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private static Page CreateFallbackErrorPage(Exception ex)
    {
        return new ContentPage
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Khong the khoi tao ung dung.",
                        HorizontalTextAlignment = TextAlignment.Center,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = ex.Message,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }
}
