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

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            window.Page = _appShell;
        }
    }
}
