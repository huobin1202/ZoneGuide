using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ZoneGuide.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserSessionService _userSessionService;
    private readonly LanguageSelectionViewModel _viewModel;

    public LanguageSelectionPage(
        LanguageSelectionViewModel viewModel,
        IServiceProvider serviceProvider,
        IUserSessionService userSessionService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _userSessionService = userSessionService;
        _viewModel.Completed += OnCompleted;
        BindingContext = viewModel;
    }

    private async void OnCompleted(object? sender, EventArgs e)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            var isLoggedIn = await _userSessionService.IsAuthenticatedAsync();
            try
            {
                window.Page = isLoggedIn
                    ? _serviceProvider.GetRequiredService<AppShell>()
                    : _serviceProvider.GetRequiredService<LoginPage>();
            }
            catch (ObjectDisposedException)
            {
                // App is being torn down, ignore navigation.
            }
        }
    }
}
