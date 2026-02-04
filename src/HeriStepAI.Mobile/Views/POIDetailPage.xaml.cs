using HeriStepAI.Mobile.ViewModels;

namespace HeriStepAI.Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
