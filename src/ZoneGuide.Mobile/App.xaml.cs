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

        _settingsService.LoadAsync().GetAwaiter().GetResult();
        
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
        var rootPage = _settingsService.Settings.HasCompletedLanguageSelection
            ? (Page)_appShell
            : _languageSelectionPage;
        var window = new Window(rootPage);
        
        // Xử lý lỗi khi tạo window
        window.Created += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("[App] Window Created successfully");
        };
        
        return window;
    }
}
