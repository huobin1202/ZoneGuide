using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.Specialized;
using System.Globalization;
#if ANDROID
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Typeface = Android.Graphics.Typeface;
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
    private Marker? _nativeUserMarker;
    private BitmapDescriptor? _userCursorMarkerIcon;
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
#if ANDROID
            else if (e.PropertyName == nameof(MapViewModel.UserLocation))
            {
                MainThread.BeginInvokeOnMainThread(RefreshNativeUserLocationMarker);
            }
#endif
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

                for (var index = 0; index < _viewModel.POIs.Count; index++)
                {
                    var poi = _viewModel.POIs[index];
                    var orderText = GetPoiOrderText(poi, index + 1);
                    var markerOptions = new MarkerOptions()
                        .SetPosition(new LatLng(poi.Latitude, poi.Longitude))
                        .SetTitle($"{orderText}. {poi.Name ?? "Không tên"}")
                        .Anchor(0.5f, 0.5f);

                    var icon = await GetPoiMarkerIconAsync(poi, orderText);
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

                RefreshNativeUserLocationMarker();

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

                for (var index = 0; index < _viewModel.POIs.Count; index++)
                {
                    var poi = _viewModel.POIs[index];
                    var orderText = GetPoiOrderText(poi, index + 1);
                    var label = _viewModel.IsTourModeActive
                        ? $"{orderText}. {poi.Name ?? "Không tên"}"
                        : poi.Name ?? "Không tên";

                    var pin = new Pin
                    {
                        Label = label,
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

    private static string GetPoiOrderText(POI poi, int fallbackOrder)
    {
        var order = poi.OrderInTour > 0 ? poi.OrderInTour : fallbackOrder;
        return order.ToString(CultureInfo.InvariantCulture);
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

        if (_nativeMap.UiSettings != null)
        {
            _nativeMap.UiSettings.MyLocationButtonEnabled = false;
        }
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

    private void RefreshNativeUserLocationMarker()
    {
        if (_nativeMap == null)
            return;

        _nativeUserMarker?.Remove();
        _nativeUserMarker = null;

        if (_viewModel.UserLocation == null)
            return;

        var markerOptions = new MarkerOptions()
            .SetPosition(new LatLng(_viewModel.UserLocation.Latitude, _viewModel.UserLocation.Longitude))
            .SetTitle("Vi tri hien tai")
            .Anchor(0.5f, 0.5f)
            .SetIcon(GetUserCursorMarkerIcon());

        _nativeUserMarker = _nativeMap.AddMarker(markerOptions);
    }

    private BitmapDescriptor GetUserCursorMarkerIcon()
    {
        if (_userCursorMarkerIcon != null)
            return _userCursorMarkerIcon;

        using var bitmap = CreateUserCursorMarkerBitmap();
        _userCursorMarkerIcon = BitmapDescriptorFactory.FromBitmap(bitmap);
        return _userCursorMarkerIcon;
    }

    private static Bitmap CreateUserCursorMarkerBitmap()
    {
        const int markerSize = 74;
        var output = Bitmap.CreateBitmap(markerSize, markerSize, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(output);

        using var backgroundPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White
        };
        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, markerSize / 2f, backgroundPaint);

        using var borderPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.ParseColor("#CBD5E1"),
            StrokeWidth = 2.5f
        };
        borderPaint.SetStyle(Android.Graphics.Paint.Style.Stroke);
        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, (markerSize / 2f) - 1.8f, borderPaint);

        using var textPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.ParseColor("#2563EB"),
            TextSize = 40f,
            TextAlign = Android.Graphics.Paint.Align.Center
        };
        textPaint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));

        canvas.Save();
        canvas.Rotate(-45f, markerSize / 2f, markerSize / 2f);
        DrawCenteredText(canvas, "➤", markerSize / 2f, markerSize / 2f + 1f, textPaint);
        canvas.Restore();

        return output;
    }

    private async Task<BitmapDescriptor?> GetPoiMarkerIconAsync(POI poi, string orderText)
    {
        var resolvedImageSource = ResolvePoiMarkerImageSource(poi);
        var cacheKey = $"{resolvedImageSource ?? "placeholder"}|{orderText}";

        if (_markerIconCache.TryGetValue(cacheKey, out var cachedDescriptor))
            return cachedDescriptor;

        Bitmap? sourceBitmap = null;
        if (!string.IsNullOrWhiteSpace(resolvedImageSource))
        {
            sourceBitmap = await LoadBitmapForMarkerAsync(resolvedImageSource);
        }

        using var iconBitmap = CreateCircularMarkerBitmap(sourceBitmap, orderText);
        sourceBitmap?.Dispose();

        var descriptor = BitmapDescriptorFactory.FromBitmap(iconBitmap);
        _markerIconCache[cacheKey] = descriptor;
        return descriptor;
    }

    private static string? ResolvePoiMarkerImageSource(POI poi)
    {
        var candidates = new[] { poi.ImagePath, poi.ImageUrl };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var resolved = POIListViewModel.ResolveImageSource(candidate);
            if (!string.IsNullOrWhiteSpace(resolved) && !string.Equals(resolved, "location.svg", StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        return null;
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

    private static Bitmap CreateCircularMarkerBitmap(Bitmap? source, string orderText)
    {
        const int markerSize = 108;
        const float outerBorderWidth = 4f;
        const float innerPadding = 6f;
        const float badgeRadius = 18f;

        var output = Bitmap.CreateBitmap(markerSize, markerSize, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(output);

        using var backgroundPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White
        };
        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, markerSize / 2f, backgroundPaint);

        var imageRadius = (markerSize / 2f) - outerBorderWidth;

        if (source != null)
        {
            using var scaled = Bitmap.CreateScaledBitmap(source, markerSize, markerSize, true);
            using var shader = new BitmapShader(scaled, Shader.TileMode.Clamp!, Shader.TileMode.Clamp!);
            using var imagePaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias);
            imagePaint.SetShader(shader);

            canvas.DrawCircle(markerSize / 2f, markerSize / 2f, imageRadius - innerPadding, imagePaint);
        }
        else
        {
            using var fallbackPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
            {
                Color = Android.Graphics.Color.ParseColor("#9CA3AF")
            };
            canvas.DrawCircle(markerSize / 2f, markerSize / 2f, imageRadius - innerPadding, fallbackPaint);
        }

        using var ringPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.ParseColor("#1D4ED8"),
            StrokeWidth = outerBorderWidth
        };
        ringPaint.SetStyle(Android.Graphics.Paint.Style.Stroke);
        canvas.DrawCircle(markerSize / 2f, markerSize / 2f, (markerSize / 2f) - (outerBorderWidth / 2f), ringPaint);

        var badgeCenterX = badgeRadius + 7f;
        var badgeCenterY = markerSize - badgeRadius - 8f;

        using var badgePaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.ParseColor("#E91E63")
        };
        canvas.DrawCircle(badgeCenterX, badgeCenterY, badgeRadius, badgePaint);

        using var badgeBorderPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White,
            StrokeWidth = 2f
        };
        badgeBorderPaint.SetStyle(Android.Graphics.Paint.Style.Stroke);
        canvas.DrawCircle(badgeCenterX, badgeCenterY, badgeRadius - 1f, badgeBorderPaint);

        using var textPaint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White,
            TextSize = 23f,
            TextAlign = Android.Graphics.Paint.Align.Center
        };
        textPaint.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
        DrawCenteredText(canvas, orderText, badgeCenterX, badgeCenterY + 0.5f, textPaint);

        return output;
    }

    private static void DrawCenteredText(Canvas canvas, string text, float centerX, float centerY, Android.Graphics.Paint paint)
    {
        var metrics = paint.GetFontMetrics();
        var baseline = centerY - ((metrics.Ascent + metrics.Descent) / 2f);
        canvas.DrawText(text, centerX, baseline, paint);
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
