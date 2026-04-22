using ZoneGuide.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ZoneGuide.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private const string FallbackRoute = "//more";

    public SettingsPage(SettingsViewModel viewModel)
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
