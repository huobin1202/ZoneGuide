using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    public TourDetailPage(TourDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
