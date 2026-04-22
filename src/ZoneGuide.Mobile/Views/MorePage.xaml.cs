using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class MorePage : ContentPage
{
    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        await QrScannerNavigationHelper.OpenScannerAsync(this);
    }
}
