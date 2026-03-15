using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
