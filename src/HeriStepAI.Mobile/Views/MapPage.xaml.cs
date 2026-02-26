using HeriStepAI.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace HeriStepAI.Mobile.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _viewModel;

    public MapPage(MapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();
            UpdateMapPins();
            UpdateMapRegion();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] Error: {ex}");
        }
    }

    private void UpdateMapPins()
    {
        MainMap.Pins.Clear();
        
        foreach (var poi in _viewModel.POIs)
        {
            var pin = new Pin
            {
                Label = poi.Name,
                Address = poi.ShortDescription,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            };
            
            pin.MarkerClicked += (s, e) =>
            {
                _viewModel.SelectPOICommand.Execute(poi);
                e.HideInfoWindow = true;
            };
            
            MainMap.Pins.Add(pin);
        }
    }

    private void UpdateMapRegion()
    {
        if (_viewModel.MapSpan != null)
        {
            MainMap.MoveToRegion(_viewModel.MapSpan);
        }
    }
}
