using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.Specialized;

namespace ZoneGuide.Mobile.Views;

public partial class MapPage : ContentPage, IQueryAttributable
{
    private readonly MapViewModel _viewModel;
    private int _lastRenderedPinCount = -1;
    private Polyline? _tourRoutePolyline;
    private bool _hasInitialized;
    private CancellationTokenSource? _pinsUpdateDebounceCts;
    private CancellationTokenSource? _routeUpdateDebounceCts;
    private const int CollectionUpdateDebounceMs = 80;
    private bool _tourOverlayRequestedOnNavigation;
    private bool _isSearchSheetOpen;

    public MapPage(MapViewModel viewModel)
    {
        _viewModel = viewModel;

        try
        {
            InitializeComponent();
            BindingContext = viewModel;
            AttachViewModelEvents();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] Init Error: {ex}");
            BindingContext = viewModel;
            Content = BuildInitializationFallback(ex.Message);
        }
    }

    private void AttachViewModelEvents()
    {
        _viewModel.POIs.CollectionChanged += OnPoisCollectionChanged;
        _viewModel.TourRoutePoints.CollectionChanged += OnTourRoutePointsCollectionChanged;
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

    private static View BuildInitializationFallback(string message)
    {
        return new Grid
        {
            Padding = new Thickness(24),
            BackgroundColor = Colors.White,
            Children =
            {
                new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 10,
                    Children =
                    {
                        new Label
                        {
                            Text = "Khong the tai giao dien ban do",
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 18,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = message,
                            FontSize = 13,
                            TextColor = Colors.Gray,
                            HorizontalTextAlignment = TextAlignment.Center
                        }
                    }
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (!_hasInitialized)
            {
                await _viewModel.InitializeAsync();
                _hasInitialized = true;
            }
            else
            {
                await _viewModel.ApplyTourRequestAsync();
            }

            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();

            if (!_tourOverlayRequestedOnNavigation)
            {
                _viewModel.IsTourPoiListVisible = false;
            }

            _tourOverlayRequestedOnNavigation = false;

            // Map control trong Shell có thể khởi tạo chậm hơn vòng đời trang.
            await Task.Delay(250);
            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();

            ResetSearchSheetLayout();
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
            var hasTourOverlayContext = startTour && tourId.HasValue;

            _tourOverlayRequestedOnNavigation = hasTourOverlayContext;
            if (!hasTourOverlayContext)
            {
                _viewModel.IsTourPoiListVisible = false;
            }

            _viewModel.SetTourRequest(tourId, startTour);

            if (!startTour || !tourId.HasValue || !_hasInitialized)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelDebounce(ref _pinsUpdateDebounceCts);
        CancelDebounce(ref _routeUpdateDebounceCts);
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

    private void OnPoisCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DebounceCollectionUpdate(ref _pinsUpdateDebounceCts, UpdateMapPins);
    }

    private void OnTourRoutePointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DebounceCollectionUpdate(ref _routeUpdateDebounceCts, UpdateTourRoute);
    }

    private void DebounceCollectionUpdate(ref CancellationTokenSource? debounceCts, Action updateAction)
    {
        CancelDebounce(ref debounceCts);

        debounceCts = new CancellationTokenSource();
        var token = debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(CollectionUpdateDebounceMs, token);
                if (token.IsCancellationRequested)
                    return;

                MainThread.BeginInvokeOnMainThread(updateAction);
            }
            catch (OperationCanceledException)
            {
                // Ignore debounced cancellations.
            }
        }, token);
    }

    private static void CancelDebounce(ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
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

    private void ResetSearchSheetLayout()
    {
        if (MapSearchSheet == null || SearchSheetBackdrop == null)
            return;

        _isSearchSheetOpen = false;
        SearchSheetBackdrop.IsVisible = false;
        SearchSheetBackdrop.Opacity = 0;
        MapSearchSheet.TranslationY = GetSearchSheetClosedTranslationY();
    }

    private double GetSearchSheetClosedTranslationY()
    {
        var sheetHeight = MapSearchSheet?.Height ?? 0;
        var pageHeight = Height;
        var height = Math.Max(sheetHeight, pageHeight);

        if (height <= 0)
            return 700;

        return height + 24;
    }

    private async Task OpenSearchSheetAsync()
    {
        if (_isSearchSheetOpen || MapSearchSheet == null || SearchSheetBackdrop == null)
            return;

        _isSearchSheetOpen = true;
        SearchSheetBackdrop.IsVisible = true;
        SearchSheetBackdrop.Opacity = 0;
        MapSearchSheet.TranslationY = GetSearchSheetClosedTranslationY();

        await Task.WhenAll(
            SearchSheetBackdrop.FadeToAsync(1, 150, Easing.CubicOut),
            MapSearchSheet.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    private async Task CloseSearchSheetAsync()
    {
        if (!_isSearchSheetOpen || MapSearchSheet == null || SearchSheetBackdrop == null)
            return;

        _isSearchSheetOpen = false;

        await Task.WhenAll(
            SearchSheetBackdrop.FadeToAsync(0, 120, Easing.CubicIn),
            MapSearchSheet.TranslateToAsync(0, GetSearchSheetClosedTranslationY(), 180, Easing.CubicIn));

        SearchSheetBackdrop.IsVisible = false;
    }

    private async void OnOpenSearchSheetTapped(object? sender, TappedEventArgs e)
    {
        await OpenSearchSheetAsync();
    }

    private async void OnCloseSearchSheetTapped(object? sender, TappedEventArgs e)
    {
        await CloseSearchSheetAsync();
    }

    private async void OnMapSearchResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not POI poi)
            return;

        _viewModel.SelectPOICommand.Execute(poi);

        await CloseSearchSheetAsync();

        try
        {
            await Shell.Current.GoToAsync($"POIDetailPage?id={poi.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] Navigate to POIDetailPage error: {ex}");
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }
    }
}
