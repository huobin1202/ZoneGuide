using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class TourListPage : ContentPage
{
    private readonly TourListViewModel _viewModel;

    public TourListPage(TourListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        await QrScannerNavigationHelper.OpenScannerAsync(this);
    }
}
