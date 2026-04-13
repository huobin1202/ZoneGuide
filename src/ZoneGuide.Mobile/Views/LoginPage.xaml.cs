using Microsoft.Maui.ApplicationModel;
using ZoneGuide.Mobile.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ZoneGuide.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
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

            try
            {
                var appShell = _serviceProvider.GetRequiredService<AppShell>();
                window.Page = appShell;
                await appShell.GoToAsync("//home");
            }
            catch (ObjectDisposedException)
            {
                // App is being torn down, ignore navigation.
            }
        });
    }
}
