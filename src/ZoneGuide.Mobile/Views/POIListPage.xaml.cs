using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class POIListPage : ContentPage
{
    private readonly POIListViewModel _viewModel;

    public POIListPage(POIListViewModel viewModel)
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
}
