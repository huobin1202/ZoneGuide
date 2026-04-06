using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.Specialized;
#if ANDROID
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
#endif

namespace ZoneGuide.Mobile.Views;

public partial class MapPage : ContentPage, IQueryAttributable
{
    private readonly MapViewModel _viewModel;
    private int _lastRenderedPinCount = -1;
    private Microsoft.Maui.Controls.Maps.Polyline? _tourRoutePolyline;
    private bool _hasInitialized;
    private CancellationTokenSource? _pinsUpdateDebounceCts;
    private CancellationTokenSource? _routeUpdateDebounceCts;
    private const int CollectionUpdateDebounceMs = 80;
    private bool _tourOverlayRequestedOnNavigation;
    private bool _isSearchSheetOpen;
    private int? _focusPoiIdRequestedOnNavigation;
#if ANDROID
    private GoogleMap? _nativeMap;
    private readonly Dictionary<Marker, POI> _nativePoiMarkers = new();
    private readonly Dictionary<string, BitmapDescriptor> _markerIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient MarkerImageHttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
#endif

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

            if (_focusPoiIdRequestedOnNavigation.HasValue)
            {
                await _viewModel.FocusPOIByIdAsync(_focusPoiIdRequestedOnNavigation.Value);
                UpdateMapPins();
                UpdateMapRegion();
                _focusPoiIdRequestedOnNavigation = null;
            }

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
            _focusPoiIdRequestedOnNavigation = TryGetIntQueryValue(query, "poiId");

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
            {
                if (_focusPoiIdRequestedOnNavigation.HasValue && _hasInitialized)
                {
                    _ = MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _viewModel.FocusPOIByIdAsync(_focusPoiIdRequestedOnNavigation.Value);
                        UpdateMapPins();
                        UpdateMapRegion();
                        _focusPoiIdRequestedOnNavigation = null;
                    });
                }

                return;
            }

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _viewModel.ApplyTourRequestAsync();

                if (_focusPoiIdRequestedOnNavigation.HasValue)
                {
                    await _viewModel.FocusPOIByIdAsync(_focusPoiIdRequestedOnNavigation.Value);
                    _focusPoiIdRequestedOnNavigation = null;
                }

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

                var polyline = new Microsoft.Maui.Controls.Maps.Polyline
                {
                    StrokeColor = Microsoft.Maui.Graphics.Color.FromArgb("#4F2DD9"),
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

#if ANDROID
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await EnsureNativeMapAsync();
                if (_nativeMap == null)
                    return;

                _nativeMap.Clear();
                _nativePoiMarkers.Clear();

                foreach (var poi in _viewModel.POIs)
                {
                    var markerOptions = new MarkerOptions()
                        .SetPosition(new LatLng(poi.Latitude, poi.Longitude))
                        .SetTitle(poi.Name ?? "Không tên");

                    var icon = await GetPoiMarkerIconAsync(poi);
                    if (icon != null)
                    {
                        markerOptions.SetIcon(icon);
                    }

                    var marker = _nativeMap.AddMarker(markerOptions);
                    if (marker != null)
                    {
                        _nativePoiMarkers[marker] = poi;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[MapPage] Android image markers rendered: {_nativePoiMarkers.Count}");

                if (_viewModel.SelectedPOI == null && _nativePoiMarkers.Count > 0 && _nativePoiMarkers.Count != _lastRenderedPinCount)
                {
                    FitMapToAllPins();
                }

                _lastRenderedPinCount = _nativePoiMarkers.Count;
            });
#else
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
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateMapPins Error: {ex}");
        }
    }

#if ANDROID
    private async Task EnsureNativeMapAsync()
    {
        if (_nativeMap != null || MainMap?.Handler?.PlatformView is not MapView mapView)
            return;

        var tcs = new TaskCompletionSource<GoogleMap>();
        mapView.GetMapAsync(new SingleMapReadyCallback(map => tcs.TrySetResult(map)));
        _nativeMap = await tcs.Task;

        _nativeMap.MarkerClick -= OnNativeMarkerClick;
        _nativeMap.MarkerClick += OnNativeMarkerClick;
    }

    private void OnNativeMarkerClick(object? sender, GoogleMap.MarkerClickEventArgs e)
    {
        if (_nativePoiMarkers.TryGetValue(e.Marker, out var poi))
        {
            _viewModel.SelectPOICommand.Execute(poi);
            e.Handled = true;
            return;
        }

        e.Handled = false;
    }

    private async Task<BitmapDescriptor?> GetPoiMarkerIconAsync(POI poi)
    {
        var resolvedImageSource = POIListViewModel.ResolveImageSource(poi.ImageUrl);
        if (string.IsNullOrWhiteSpace(resolvedImageSource))
            return null;

        if (_markerIconCache.TryGetValue(resolvedImageSource, out var cachedDescriptor))
            return cachedDescriptor;

        var sourceBitmap = await LoadBitmapForMarkerAsync(resolvedImageSource);
        if (sourceBitmap == null)
            return null;

        using var iconBitmap = CreateCircularMarkerBitmap(sourceBitmap);
        sourceBitmap.Dispose();

        var descriptor = BitmapDescriptorFactory.FromBitmap(iconBitmap);
        _markerIconCache[resolvedImageSource] = descriptor;
        return descriptor;
    }

    private static async Task<Bitmap?> LoadBitmapForMarkerAsync(string imageSource)
    {
        try
        {
            if (Uri.TryCreate(imageSource, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    var bytes = await MarkerImageHttpClient.GetByteArrayAsync(uri);
                    return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                }

                if (uri.IsFile && File.Exists(uri.LocalPath))
                {
                    return BitmapFactory.DecodeFile(uri.LocalPath);
                }
            }

            if (File.Exists(imageSource))
            {
                return BitmapFactory.DecodeFile(imageSource);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] LoadBitmapForMarkerAsync failed for {imageSource}: {ex.Message}");
        }

        return null;
    }

    private static Bitmap CreateCircularMarkerBitmap(Bitmap source)
    {
        const int markerSize = 96;
        const float borderWidth = 4f;

        using var scaled = Bitmap.CreateScaledBitmap(source, markerSize, markerSize, true);
    var output = Bitmap.CreateBitmap(markerSize, markerSize, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(output);

        using var borderPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White
        };
        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, markerSize / 2f, borderPaint);

        using var shader = new BitmapShader(scaled, Shader.TileMode.Clamp!, Shader.TileMode.Clamp!);
        using var imagePaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            AntiAlias = true
        };
        imagePaint.SetShader(shader);

        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, (markerSize / 2f) - borderWidth, imagePaint);
        return output;
    }

    private sealed class SingleMapReadyCallback : Java.Lang.Object, IOnMapReadyCallback
    {
        private readonly Action<GoogleMap> _onMapReady;

        public SingleMapReadyCallback(Action<GoogleMap> onMapReady)
        {
            _onMapReady = onMapReady;
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _onMapReady.Invoke(googleMap);
        }
    }
#endif

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
#if ANDROID
            var poiPoints = _viewModel.POIs
                .Select(p => new Location(p.Latitude, p.Longitude))
                .ToList();

            if (MainMap == null || poiPoints.Count == 0)
                return;

            if (poiPoints.Count == 1)
            {
                MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(poiPoints[0], Distance.FromKilometers(1)));
                return;
            }

            var minLatAndroid = poiPoints.Min(p => p.Latitude);
            var maxLatAndroid = poiPoints.Max(p => p.Latitude);
            var minLonAndroid = poiPoints.Min(p => p.Longitude);
            var maxLonAndroid = poiPoints.Max(p => p.Longitude);

            var centerAndroid = new Location((minLatAndroid + maxLatAndroid) / 2, (minLonAndroid + maxLonAndroid) / 2);
            var verticalKmAndroid = MapViewModelCalculateDistanceKm(minLatAndroid, centerAndroid.Longitude, maxLatAndroid, centerAndroid.Longitude);
            var horizontalKmAndroid = MapViewModelCalculateDistanceKm(centerAndroid.Latitude, minLonAndroid, centerAndroid.Latitude, maxLonAndroid);
            var radiusKmAndroid = Math.Max(1.0, Math.Max(verticalKmAndroid, horizontalKmAndroid) * 0.8 + 0.7);

            MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(centerAndroid, Distance.FromKilometers(radiusKmAndroid)));
#else
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
#endif
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
