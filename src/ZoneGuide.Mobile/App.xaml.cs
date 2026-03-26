using Microsoft.Maui.ApplicationModel;
using ZoneGuide.Mobile.Views;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile;

public partial class App : Application
{
    private readonly ISettingsService _settingsService;
    private readonly AppShell _appShell;
    private readonly LanguageSelectionPage _languageSelectionPage;

    public App(ISettingsService settingsService, AppShell appShell, LanguageSelectionPage languageSelectionPage)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _appShell = appShell;
        _languageSelectionPage = languageSelectionPage;

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

            var rootPage = _settingsService.Settings.HasCompletedLanguageSelection
                ? (Page)_appShell
                : _languageSelectionPage;

            await MainThread.InvokeOnMainThreadAsync(() => window.Page = rootPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Root initialization failed: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() => window.Page = _languageSelectionPage);
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
}
