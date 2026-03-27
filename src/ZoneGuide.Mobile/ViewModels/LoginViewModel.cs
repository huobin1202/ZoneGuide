using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;

namespace ZoneGuide.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUserSessionService _userSessionService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public event EventHandler? LoginSucceeded;

    public LoginViewModel(IUserSessionService userSessionService)
    {
        _userSessionService = userSessionService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = AppLocalizer.Instance.Translate("login_required");
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _userSessionService.LoginAsync(Email, Password);
            if (!result.Success)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? AppLocalizer.Instance.Translate("login_failed")
                    : result.Message;
                return;
            }

            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{AppLocalizer.Instance.Translate("login_error_prefix")}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
