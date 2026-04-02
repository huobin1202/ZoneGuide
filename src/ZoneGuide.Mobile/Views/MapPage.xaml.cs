using ZoneGuide.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace ZoneGuide.Mobile.Views;

public partial class MapPage : ContentPage, IQueryAttributable
{
    private readonly MapViewModel _viewModel;
    private int _lastRenderedPinCount = -1;
    private Polyline? _tourRoutePolyline;

    public MapPage(MapViewModel viewModel)
    {
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            
            _viewModel.POIs.CollectionChanged += (s, e) => UpdateMapPins();
            _viewModel.TourRoutePoints.CollectionChanged += (s, e) => UpdateTourRoute();
            _viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(MapViewModel.MapSpan))
                {
                    UpdateMapRegion();
                }
                else if (e.PropertyName == nameof(MapViewModel.SelectedPOI) && _viewModel.SelectedPOI == null)
                {
                    FitMapToAllPins();
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] Init Error: {ex}");
            _viewModel = viewModel;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();
            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();

            // Map control trong Shell có thể khởi tạo chậm hơn vòng đời trang.
            await Task.Delay(250);
            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] OnAppearing Error: {ex}");
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            var tourId = TryGetIntQueryValue(query, "tourId");
            var startTour = TryGetBoolQueryValue(query, "startTour");

            _viewModel.SetTourRequest(tourId, startTour);

            if (!startTour || !tourId.HasValue)
                return;

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _viewModel.ApplyTourRequestAsync();
                UpdateMapPins();
                UpdateTourRoute();
                UpdateMapRegion();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] ApplyQueryAttributes error: {ex.Message}");
        }
    }

    private static int? TryGetIntQueryValue(IDictionary<string, object> query, string key)
    {
        var value = TryGetStringQueryValue(query, key);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool TryGetBoolQueryValue(IDictionary<string, object> query, string key)
    {
        var value = TryGetStringQueryValue(query, key);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetStringQueryValue(IDictionary<string, object> query, string key)
    {
        if (!query.TryGetValue(key, out var value) || value == null)
            return null;

        return Uri.UnescapeDataString(value.ToString() ?? string.Empty);
    }

    private void UpdateTourRoute()
    {
        try
        {
            if (MainMap == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_tourRoutePolyline != null)
                {
                    MainMap.MapElements.Remove(_tourRoutePolyline);
                    _tourRoutePolyline = null;
                }

                if (_viewModel.TourRoutePoints.Count < 2)
                    return;

                var polyline = new Polyline
                {
                    StrokeColor = Color.FromArgb("#4F2DD9"),
                    StrokeWidth = 6
                };

                foreach (var point in _viewModel.TourRoutePoints)
                {
                    polyline.Geopath.Add(point);
                }

                MainMap.MapElements.Add(polyline);
                _tourRoutePolyline = polyline;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateTourRoute Error: {ex}");
        }
    }

    private void UpdateMapPins()
    {
        try
        {
            if (MainMap == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MainMap.Pins.Clear();

                foreach (var poi in _viewModel.POIs)
                {
                    var pin = new Pin
                    {
                        Label = poi.Name ?? "Không tên",
                        Address = poi.TTSScript ?? poi.FullDescription ?? poi.ShortDescription ?? "",
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

                System.Diagnostics.Debug.WriteLine($"[MapPage] Pins rendered: {MainMap.Pins.Count}");

                if (_viewModel.SelectedPOI == null && MainMap.Pins.Count > 0 && MainMap.Pins.Count != _lastRenderedPinCount)
                {
                    FitMapToAllPins();
                }

                _lastRenderedPinCount = MainMap.Pins.Count;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateMapPins Error: {ex}");
        }
    }

    private void UpdateMapRegion()
    {
        try
        {
            if (MainMap == null) return;
            
            if (_viewModel.MapSpan != null)
            {
                MainMap.MoveToRegion(_viewModel.MapSpan);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateMapRegion Error: {ex}");
        }
    }

    private void FitMapToAllPins()
    {
        try
        {
            if (MainMap == null || MainMap.Pins.Count == 0)
                return;

            if (MainMap.Pins.Count == 1)
            {
                var single = MainMap.Pins[0].Location;
                MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(single, Distance.FromKilometers(1)));
                return;
            }

            var minLat = MainMap.Pins.Min(p => p.Location.Latitude);
            var maxLat = MainMap.Pins.Max(p => p.Location.Latitude);
            var minLon = MainMap.Pins.Min(p => p.Location.Longitude);
            var maxLon = MainMap.Pins.Max(p => p.Location.Longitude);

            var center = new Location((minLat + maxLat) / 2, (minLon + maxLon) / 2);

            var verticalKm = MapViewModelCalculateDistanceKm(minLat, center.Longitude, maxLat, center.Longitude);
            var horizontalKm = MapViewModelCalculateDistanceKm(center.Latitude, minLon, center.Latitude, maxLon);
            var radiusKm = Math.Max(1.0, Math.Max(verticalKm, horizontalKm) * 0.8 + 0.7);

            MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radiusKm)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] FitMapToAllPins Error: {ex}");
        }
    }

    private static double MapViewModelCalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }
}
