using Microsoft.Maui.ApplicationModel;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly AppShell _appShell;
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel, AppShell appShell)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appShell = appShell;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.LoginSucceeded -= OnLoginSucceeded;
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        _ = SwitchToShellAsync();
    }

    private async Task SwitchToShellAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window == null)
                return;

            window.Page = _appShell;
            await _appShell.GoToAsync("//pois");
        });
    }
}
