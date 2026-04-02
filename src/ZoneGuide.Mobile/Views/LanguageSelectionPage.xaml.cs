using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private readonly AppShell _appShell;
    private readonly LanguageSelectionViewModel _viewModel;

    public LanguageSelectionPage(
        LanguageSelectionViewModel viewModel,
        AppShell appShell)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appShell = appShell;
        _viewModel.Completed += OnCompleted;
        BindingContext = viewModel;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            window.Page = _appShell;
        }
    }
}
