namespace ZoneGuide.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
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
        var window = new Window(new AppShell());
        
        // Xử lý lỗi khi tạo window
        window.Created += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("[App] Window Created successfully");
        };
        
        return window;
    }
}
