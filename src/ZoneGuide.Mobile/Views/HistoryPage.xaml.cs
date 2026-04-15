using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;
    private const string FallbackRoute = "//more";

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        if (await TryGoBackAsync())
            return;

        await Shell.Current.GoToAsync(FallbackRoute);
    }

    private static async Task<bool> TryGoBackAsync()
    {
        var shell = Shell.Current;
        if (shell?.Navigation?.NavigationStack?.Count > 1)
        {
            await shell.GoToAsync("..");
            return true;
        }

        return false;
    }
}
