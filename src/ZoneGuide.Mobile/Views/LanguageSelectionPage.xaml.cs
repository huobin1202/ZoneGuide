using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Mobile.Services;

namespace ZoneGuide.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private readonly AppShell _appShell;
    private readonly LoginPage _loginPage;
    private readonly IUserSessionService _userSessionService;
    private readonly LanguageSelectionViewModel _viewModel;

    public LanguageSelectionPage(
        LanguageSelectionViewModel viewModel,
        AppShell appShell,
        LoginPage loginPage,
        IUserSessionService userSessionService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appShell = appShell;
        _loginPage = loginPage;
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
            window.Page = isLoggedIn ? _appShell : _loginPage;
        }
    }
}
