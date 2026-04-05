using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    private POIDetailViewModel? _viewModel;

    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnBindingContextChanged()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnBindingContextChanged();

        _viewModel = BindingContext as POIDetailViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RenderMiniMap();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderMiniMap();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(POIDetailViewModel.CurrentPoi))
            return;

        MainThread.BeginInvokeOnMainThread(RenderMiniMap);
    }

    private void RenderMiniMap()
    {
        if (MiniPoiMap == null)
            return;

        MiniPoiMap.Pins.Clear();

        var poi = _viewModel?.CurrentPoi;
        if (poi == null)
            return;

        if (poi.Latitude is < -90 or > 90 || poi.Longitude is < -180 or > 180)
            return;

        var poiLocation = new Location(poi.Latitude, poi.Longitude);
        MiniPoiMap.Pins.Add(new Pin
        {
            Label = poi.Name,
            Address = poi.ShortDescription,
            Location = poiLocation,
            Type = PinType.Place
        });

        MiniPoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(poiLocation, Distance.FromMeters(220)));
    }
}
