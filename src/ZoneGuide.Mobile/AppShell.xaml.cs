using ZoneGuide.Mobile.Views;

namespace ZoneGuide.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes
        Routing.RegisterRoute(nameof(POIDetailPage), typeof(POIDetailPage));
        Routing.RegisterRoute(nameof(TourDetailPage), typeof(TourDetailPage));
    }
}
