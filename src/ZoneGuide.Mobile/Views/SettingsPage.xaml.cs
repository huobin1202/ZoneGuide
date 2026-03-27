using ZoneGuide.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ZoneGuide.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public SettingsPage(SettingsViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _viewModel.LogoutRequested += OnLogoutRequested;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window == null)
            return;

        var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
        window.Page = loginPage;
    }
}
