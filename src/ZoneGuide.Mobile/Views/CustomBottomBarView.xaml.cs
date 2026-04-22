namespace ZoneGuide.Mobile.Views;

public partial class CustomBottomBarView : ContentView
{
    public static readonly BindableProperty CurrentTabProperty =
        BindableProperty.Create(
            nameof(CurrentTab),
            typeof(string),
            typeof(CustomBottomBarView),
            "home",
            propertyChanged: OnCurrentTabChanged);

    public string CurrentTab
    {
        get => (string)GetValue(CurrentTabProperty);
        set => SetValue(CurrentTabProperty, value);
    }

    public CustomBottomBarView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySelectionState();
    }

    private static void OnCurrentTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomBottomBarView view)
        {
            view.ApplySelectionState();
        }
    }

    private void ApplySelectionState()
    {
        ApplyTabState(HomeIcon, HomeText, IsActive("home"));
        ApplyTabState(MapIcon, MapText, IsActive("map"));
        ApplyTabState(ToursIcon, ToursText, IsActive("tours"));
        ApplyTabState(MoreIcon, MoreText, IsActive("more"));
    }

    private bool IsActive(string tab) => string.Equals(CurrentTab, tab, StringComparison.OrdinalIgnoreCase);

    private static void ApplyTabState(Label icon, Label text, bool isActive)
    {
        var color = isActive ? Color.FromArgb("#7C3AED") : Color.FromArgb("#A1A1AA");
        icon.TextColor = color;
        text.TextColor = color;
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("//home", "home");
    }

    private async void OnMapTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("//map", "map");
    }

    private async void OnToursTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("//tours", "tours");
    }

    private async void OnMoreTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("//more", "more");
    }

    private async void OnQrTapped(object? sender, TappedEventArgs e)
    {
        var page = FindParentPage();
        if (page == null)
            return;

        await QrScannerNavigationHelper.OpenScannerAsync(page);
    }

    private async Task NavigateToAsync(string route, string tab)
    {
        if (IsActive(tab))
            return;

        await Shell.Current.GoToAsync(route);
    }

    private Page? FindParentPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return null;
    }
}
