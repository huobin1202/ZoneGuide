using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ZoneGuide.Mobile.Views;

public partial class LanguageSelectionPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LanguageSelectionViewModel _viewModel;

    public LanguageSelectionPage(
        LanguageSelectionViewModel viewModel,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _viewModel.Completed += OnCompleted;
        BindingContext = viewModel;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            try
            {
                window.Page = _serviceProvider.GetRequiredService<AppShell>();
            }
            catch (ObjectDisposedException)
            {
                // App is being torn down, ignore navigation.
            }
        }
    }
}
