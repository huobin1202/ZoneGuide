using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.ComponentModel;
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
    private const double MapPoiSheetHandleOnlyHeight = 50;
    private const double MapPoiSheetDefaultRatio = 0.5;
    private const double MapPoiSheetExpandedRatio = 0.72;
    private const double TourSheetExpandedHeight = 296;
    private const double TourSheetCollapsedHeight = 122;
    private const uint TourSheetSnapAnimationMs = 240;
    private const double SelectedPoiOverlayBottomMargin = 16;
    private const double SelectedPoiOverlaySheetGap = 14;
    private const double BottomBarSafeInset = 92;
    private const double MiniPlayerGap = 4;
    private const double BottomSheetMiniPlayerGap = 24;
    private readonly MapViewModel _viewModel;
    private readonly GlobalMiniPlayerViewModel _miniPlayerViewModel;
    private int _lastRenderedPinCount = -1;
    private Microsoft.Maui.Controls.Maps.Polyline? _tourRoutePolyline;
    private bool _hasInitialized;
    private CancellationTokenSource? _pinsUpdateDebounceCts;
    private CancellationTokenSource? _routeUpdateDebounceCts;
    private const int CollectionUpdateDebounceMs = 80;
    private bool _tourOverlayRequestedOnNavigation;
    private bool _isMapPoiSheetPanning;
    private int? _focusPoiIdRequestedOnNavigation;
    private bool _autoPlayRequestedOnNavigation;
    private bool _openSearchRequestedOnNavigation;
    private bool _startNavigationToPoiRequestedOnNavigation;
    private bool _isTourSheetPanning;
    private double _tourSheetPanStartHeight;
    private double _mapPoiSheetPanStartHeight;
    private bool _eventsAttached;
    private const double OffscreenIndicatorSize = 48;
    private const double OffscreenIndicatorEdgePadding = 14;
    private const double OffscreenSafeViewportRatio = 0.85;
#if ANDROID
    private GoogleMap? _nativeMap;
    private readonly Dictionary<string, POI> _nativePoiMarkers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BitmapDescriptor> _markerIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient MarkerImageHttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
    private Marker? _nativeUserMarker;
    private BitmapDescriptor? _userCursorMarkerIcon;
#endif

    public MapPage(MapViewModel viewModel, GlobalMiniPlayerViewModel miniPlayerViewModel)
    {
        _viewModel = viewModel;
        _miniPlayerViewModel = miniPlayerViewModel;

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
        if (_eventsAttached)
            return;

        if (MainMap != null)
        {
            MainMap.PropertyChanged += OnMainMapPropertyChanged;
        }

        if (MapMiniPlayerView != null)
        {
            MapMiniPlayerView.SizeChanged += OnMiniPlayerLayoutChanged;
        }

        _miniPlayerViewModel.PropertyChanged += OnMiniPlayerPropertyChanged;
        _viewModel.POIs.CollectionChanged += OnPoisCollectionChanged;
        _viewModel.TourRoutePoints.CollectionChanged += OnTourRoutePointsCollectionChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _eventsAttached = true;
    }

    private void DetachViewModelEvents()
    {
        if (!_eventsAttached)
            return;

        if (MainMap != null)
        {
            MainMap.PropertyChanged -= OnMainMapPropertyChanged;
        }

        if (MapMiniPlayerView != null)
        {
            MapMiniPlayerView.SizeChanged -= OnMiniPlayerLayoutChanged;
        }

        _miniPlayerViewModel.PropertyChanged -= OnMiniPlayerPropertyChanged;
        _viewModel.POIs.CollectionChanged -= OnPoisCollectionChanged;
        _viewModel.TourRoutePoints.CollectionChanged -= OnTourRoutePointsCollectionChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _eventsAttached = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.MapSpan))
        {
            UpdateMapRegion();
        }
        else if (e.PropertyName == nameof(MapViewModel.SelectedPOI) && _viewModel.SelectedPOI == null)
        {
            FitMapToAllPins();
        }
        else if (e.PropertyName == nameof(MapViewModel.IsTourPoiListVisible))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_viewModel.IsTourPoiListVisible)
                {
                    ApplyTourSheetState(TourSheetExpandedHeight, animate: false);
                    UpdateTourRecenterButtonVisibility();
                    return;
                }

                UpdateSelectedPoiOverlayMargin();
                UpdateMapZoomControlsMargin();
                UpdateTourRecenterButtonVisibility();
            });
        }
        else if (e.PropertyName == nameof(MapViewModel.IsSelectedPoiPlayerVisible))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateSelectedPoiOverlayMargin();
                UpdateMapZoomControlsMargin();
            });
        }
        else if (e.PropertyName == nameof(MapViewModel.IsTourModeActive) ||
                 e.PropertyName == nameof(MapViewModel.UserLocation))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_viewModel.IsTourModeActive)
                {
                    CloseTourSearchOverlay();
                }

                UpdateTourRecenterButtonVisibility();
            });
        }
#if ANDROID
        if (e.PropertyName == nameof(MapViewModel.UserLocation))
        {
            MainThread.BeginInvokeOnMainThread(RefreshNativeUserLocationMarker);
        }
#endif
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
        AttachViewModelEvents();
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

            if (!_autoPlayRequestedOnNavigation)
            {
                await _viewModel.SuppressAutoNarrationForCurrentInRangePoisAsync();
            }

            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();
            UpdateTourRecenterButtonVisibility();
            UpdateMapZoomControlsMargin();

            if (_focusPoiIdRequestedOnNavigation.HasValue)
            {
                var focused = false;
                if (_startNavigationToPoiRequestedOnNavigation)
                {
                    focused = await _viewModel.PrepareInAppNavigationToPoiAsync(_focusPoiIdRequestedOnNavigation.Value);
                }
                else
                {
                    focused = await _viewModel.FocusPOIByIdAsync(
                        _focusPoiIdRequestedOnNavigation.Value,
                        allowServerSync: _autoPlayRequestedOnNavigation);
                }

                if (_autoPlayRequestedOnNavigation && focused && _viewModel.PlaySelectedPOICommand.CanExecute(null))
                {
                    await _viewModel.PlaySelectedPOICommand.ExecuteAsync(null);
                }

                UpdateMapPins();
                UpdateMapRegion();
                _focusPoiIdRequestedOnNavigation = null;
                _startNavigationToPoiRequestedOnNavigation = false;
                _autoPlayRequestedOnNavigation = false;
            }

            if (!_tourOverlayRequestedOnNavigation)
            {
                if (!_viewModel.IsTourModeActive)
                {
                    _viewModel.IsTourPoiListVisible = false;
                }
            }
            else if (_viewModel.IsTourModeActive)
            {
                _viewModel.IsTourPoiListVisible = true;
            }

            _tourOverlayRequestedOnNavigation = false;

            UpdateSelectedPoiOverlayMargin();

            // Map control trong Shell có thể khởi tạo chậm hơn vòng đời trang.
            await Task.Delay(250);
            UpdateMapPins();
            UpdateTourRoute();
            UpdateMapRegion();
            UpdateTourRecenterButtonVisibility();
            UpdateMapZoomControlsMargin();

            ResetMapPoiSheetLayout();

            if (_openSearchRequestedOnNavigation)
            {
                await OpenMapPoiSheetAsync(focusSearch: true);
                _openSearchRequestedOnNavigation = false;
            }
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
            _openSearchRequestedOnNavigation = TryGetBoolQueryValue(query, "openSearch");
            _startNavigationToPoiRequestedOnNavigation = TryGetBoolQueryValue(query, "navigate");
            _autoPlayRequestedOnNavigation = TryGetBoolQueryValue(query, "autoplay");
            var hasTourOverlayContext = startTour && tourId.HasValue;

            _tourOverlayRequestedOnNavigation = hasTourOverlayContext;
            if (!hasTourOverlayContext && !_viewModel.IsTourModeActive)
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
                        var focused = false;
                        if (_startNavigationToPoiRequestedOnNavigation)
                        {
                            focused = await _viewModel.PrepareInAppNavigationToPoiAsync(_focusPoiIdRequestedOnNavigation.Value);
                        }
                        else
                        {
                            focused = await _viewModel.FocusPOIByIdAsync(
                                _focusPoiIdRequestedOnNavigation.Value,
                                allowServerSync: _autoPlayRequestedOnNavigation);
                        }

                        if (_autoPlayRequestedOnNavigation && focused && _viewModel.PlaySelectedPOICommand.CanExecute(null))
                        {
                            await _viewModel.PlaySelectedPOICommand.ExecuteAsync(null);
                        }

                        UpdateMapPins();
                        UpdateMapRegion();
                        _focusPoiIdRequestedOnNavigation = null;
                        _startNavigationToPoiRequestedOnNavigation = false;
                        _autoPlayRequestedOnNavigation = false;
                    });
                }

                return;
            }

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _viewModel.ApplyTourRequestAsync();

                if (_focusPoiIdRequestedOnNavigation.HasValue)
                {
                    var focused = false;
                    if (_startNavigationToPoiRequestedOnNavigation)
                    {
                        focused = await _viewModel.PrepareInAppNavigationToPoiAsync(_focusPoiIdRequestedOnNavigation.Value);
                    }
                    else
                    {
                        focused = await _viewModel.FocusPOIByIdAsync(
                            _focusPoiIdRequestedOnNavigation.Value,
                            allowServerSync: _autoPlayRequestedOnNavigation);
                    }

                    if (_autoPlayRequestedOnNavigation && focused && _viewModel.PlaySelectedPOICommand.CanExecute(null))
                    {
                        await _viewModel.PlaySelectedPOICommand.ExecuteAsync(null);
                    }

                    _focusPoiIdRequestedOnNavigation = null;
                    _startNavigationToPoiRequestedOnNavigation = false;
                    _autoPlayRequestedOnNavigation = false;
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
        DetachViewModelEvents();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateTourRecenterButtonVisibility();
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
                    StrokeColor = Microsoft.Maui.Graphics.Color.FromArgb("#7C3AED"),
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
                    if (marker != null && !string.IsNullOrWhiteSpace(marker.Id))
                    {
                        _nativePoiMarkers[marker.Id] = poi;
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
                        Address = poi.TTSScript ?? "",
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

    private void OnTourSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (TourPoiBottomSheet == null)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isTourSheetPanning = true;
                _tourSheetPanStartHeight = TourPoiBottomSheet.HeightRequest > 0
                    ? TourPoiBottomSheet.HeightRequest
                    : TourSheetExpandedHeight;
                break;

            case GestureStatus.Running:
                if (!_isTourSheetPanning)
                    return;

                var nextHeight = _tourSheetPanStartHeight - e.TotalY;
                if (nextHeight < TourSheetCollapsedHeight)
                    nextHeight = TourSheetCollapsedHeight;
                if (nextHeight > TourSheetExpandedHeight)
                    nextHeight = TourSheetExpandedHeight;

                TourPoiBottomSheet.HeightRequest = nextHeight;
                if (TourPoiCollectionView != null)
                {
                    TourPoiCollectionView.IsVisible = nextHeight > (TourSheetCollapsedHeight + 12);
                    // Thu nhỏ chiều cao các card theo chiều cao sheet
                    double minCardHeight = 60;
                    double maxCardHeight = 202;
                    double minSheet = TourSheetCollapsedHeight;
                    double maxSheet = TourSheetExpandedHeight;
                    double cardHeight = minCardHeight + (maxCardHeight - minCardHeight) * ((nextHeight - minSheet) / (maxSheet - minSheet));
                    cardHeight = Math.Clamp(cardHeight, minCardHeight, maxCardHeight);
                    foreach (var item in TourPoiCollectionView.ItemsSource)
                    {
                        if (TourPoiCollectionView.ItemTemplate.CreateContent() is Border border)
                        {
                            border.HeightRequest = cardHeight;
                        }
                    }
                }
                UpdateSelectedPoiOverlayMargin(nextHeight);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isTourSheetPanning)
                    return;

                _isTourSheetPanning = false;
                var midpoint = (TourSheetExpandedHeight + TourSheetCollapsedHeight) / 2d;
                var targetHeight = Math.Abs(e.TotalY) > 10
                    ? (e.TotalY > 0 ? TourSheetCollapsedHeight : TourSheetExpandedHeight)
                    : (TourPoiBottomSheet.HeightRequest >= midpoint ? TourSheetExpandedHeight : TourSheetCollapsedHeight);

                ApplyTourSheetState(targetHeight, animate: true);
                break;
        }
    }

    private void ApplyTourSheetState(double targetHeight, bool animate)
    {
        if (TourPoiBottomSheet == null)
            return;

        var startHeight = TourPoiBottomSheet.HeightRequest > 0
            ? TourPoiBottomSheet.HeightRequest
            : targetHeight;

        if (!animate || Math.Abs(startHeight - targetHeight) < 0.5d)
        {
            TourPoiBottomSheet.HeightRequest = targetHeight;
            if (TourPoiCollectionView != null)
            {
                TourPoiCollectionView.IsVisible = targetHeight > (TourSheetCollapsedHeight + 12);
            }

            UpdateSelectedPoiOverlayMargin(targetHeight);
            return;
        }

        this.AbortAnimation("TourPoiBottomSheetSnap");
        var animation = new Animation(v =>
        {
            TourPoiBottomSheet.HeightRequest = v;
            if (TourPoiCollectionView != null)
            {
                TourPoiCollectionView.IsVisible = v > (TourSheetCollapsedHeight + 12);
            }

            UpdateSelectedPoiOverlayMargin(v);
        }, startHeight, targetHeight);

        animation.Commit(this, "TourPoiBottomSheetSnap", 16, TourSheetSnapAnimationMs, Easing.SinOut);
    }

    private void OnMapPoiSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (MapPoiBottomSheet == null || _viewModel.IsTourModeActive)
            return;

        var defaultHeight = GetMapPoiSheetDefaultHeight();
        var expandedHeight = GetMapPoiSheetExpandedHeight();

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isMapPoiSheetPanning = true;
                _mapPoiSheetPanStartHeight = MapPoiBottomSheet.HeightRequest > 0
                    ? MapPoiBottomSheet.HeightRequest
                    : defaultHeight;
                break;

            case GestureStatus.Running:
                if (!_isMapPoiSheetPanning)
                    return;

                var nextHeight = _mapPoiSheetPanStartHeight - e.TotalY;
                nextHeight = Math.Clamp(nextHeight, MapPoiSheetHandleOnlyHeight, expandedHeight);
                MapPoiBottomSheet.HeightRequest = nextHeight;
                UpdateMapPoiSheetContentVisibility(nextHeight);
                UpdateSelectedPoiOverlayMargin(nextHeight);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isMapPoiSheetPanning)
                    return;

                _isMapPoiSheetPanning = false;
                var targetHeight = ResolveNearestMapPoiSheetSnapHeight(MapPoiBottomSheet.HeightRequest, defaultHeight, expandedHeight);

                ApplyMapPoiSheetState(targetHeight, animate: true);
                break;
        }
    }

    private void ApplyMapPoiSheetState(double targetHeight, bool animate)
    {
        if (MapPoiBottomSheet == null)
            return;

        var startHeight = MapPoiBottomSheet.HeightRequest > 0
            ? MapPoiBottomSheet.HeightRequest
            : targetHeight;

        if (!animate || Math.Abs(startHeight - targetHeight) < 0.5d)
        {
            MapPoiBottomSheet.HeightRequest = targetHeight;
            UpdateMapPoiSheetContentVisibility(targetHeight);
            UpdateSelectedPoiOverlayMargin(targetHeight);
            return;
        }

        this.AbortAnimation("MapPoiBottomSheetSnap");
        var animation = new Animation(v =>
        {
            MapPoiBottomSheet.HeightRequest = v;
            UpdateMapPoiSheetContentVisibility(v);
            UpdateSelectedPoiOverlayMargin(v);
        }, startHeight, targetHeight);

        animation.Commit(this, "MapPoiBottomSheetSnap", 16, TourSheetSnapAnimationMs, Easing.SinOut);
    }

    private void UpdateSelectedPoiOverlayMargin(double? currentSheetHeight = null)
    {
        if (SelectedPoiOverlay == null)
            return;

        UpdateBottomSheetInsets();

        var bottom = Math.Max(SelectedPoiOverlayBottomMargin, BottomBarSafeInset);
        var miniPlayerInset = ResolveMiniPlayerInset();
        if (miniPlayerInset > 0)
        {
            bottom = Math.Max(bottom, miniPlayerInset + MiniPlayerGap);
        }

        var activeSheetHeight = ResolveActiveBottomSheetHeight(currentSheetHeight);
        if (activeSheetHeight.HasValue)
        {
            var sheetBottomInset = ResolveActiveBottomSheetBottomInset(miniPlayerInset);
            bottom = Math.Max(bottom, activeSheetHeight.Value + sheetBottomInset + SelectedPoiOverlaySheetGap);
        }

        var current = SelectedPoiOverlay.Margin;
        SelectedPoiOverlay.Margin = new Thickness(current.Left, current.Top, current.Right, bottom);
        UpdateMapZoomControlsMargin(currentSheetHeight);
    }

    private void UpdateMapZoomControlsMargin(double? currentSheetHeight = null)
    {
        if (MapZoomControls == null)
            return;

        var bottom = 16d;

        if (_viewModel.IsSelectedPoiPlayerVisible && SelectedPoiOverlay != null)
        {
            var overlayHeight = SelectedPoiOverlay.Height > 0 ? SelectedPoiOverlay.Height : 98;
            var overlayBottom = SelectedPoiOverlay.Margin.Bottom;
            bottom = Math.Max(bottom, overlayBottom + overlayHeight + 12);
        }

        var activeSheetHeight = ResolveActiveBottomSheetHeight(currentSheetHeight);
        if (activeSheetHeight.HasValue)
        {
            bottom = Math.Max(bottom, activeSheetHeight.Value + 12);
        }

        var miniPlayerInset = ResolveMiniPlayerInset();
        if (miniPlayerInset > 0)
        {
            bottom = Math.Max(bottom, miniPlayerInset + MiniPlayerGap);
        }

        var current = MapZoomControls.Margin;
        MapZoomControls.Margin = new Thickness(current.Left, current.Top, current.Right, bottom);
    }

    private void UpdateBottomSheetInsets()
    {
        var miniPlayerInset = ResolveMiniPlayerInset();
        var bottomInset = miniPlayerInset > 0 ? miniPlayerInset + BottomSheetMiniPlayerGap : 0;

        if (TourPoiBottomSheet != null)
        {
            var margin = TourPoiBottomSheet.Margin;
            TourPoiBottomSheet.Margin = new Thickness(margin.Left, margin.Top, margin.Right, bottomInset);
        }

        if (MapPoiBottomSheet != null)
        {
            var margin = MapPoiBottomSheet.Margin;
            MapPoiBottomSheet.Margin = new Thickness(margin.Left, margin.Top, margin.Right, 0);
        }
    }

    private double ResolveMiniPlayerInset()
    {
        if (MapMiniPlayerView == null || !_miniPlayerViewModel.IsVisible || !MapMiniPlayerView.IsVisible)
            return 0;

        var height = MapMiniPlayerView.Height > 0 ? MapMiniPlayerView.Height : MapMiniPlayerView.HeightRequest;
        if (height <= 0)
            height = 96;

        var margin = MapMiniPlayerView.Margin;
        return height + margin.Top + margin.Bottom;
    }

    private void OnMiniPlayerLayoutChanged(object? sender, EventArgs e)
    {
        UpdateSelectedPoiOverlayMargin();
    }

    private void OnMiniPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GlobalMiniPlayerViewModel.IsVisible))
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateSelectedPoiOverlayMargin());
        }
    }

    private double ResolveActiveBottomSheetBottomInset(double miniPlayerInset)
    {
        if (_viewModel.IsTourPoiListVisible)
        {
            return miniPlayerInset > 0 ? miniPlayerInset + BottomSheetMiniPlayerGap : 0;
        }

        return 0;
    }

    private double? ResolveActiveBottomSheetHeight(double? currentSheetHeight = null)
    {
        if (_viewModel.IsTourPoiListVisible && TourPoiBottomSheet != null)
        {
            return currentSheetHeight
                ?? (TourPoiBottomSheet.HeightRequest > 0 ? TourPoiBottomSheet.HeightRequest : TourSheetCollapsedHeight);
        }

        if (!_viewModel.IsTourModeActive && MapPoiBottomSheet != null && MapPoiBottomSheet.IsVisible)
        {
            return currentSheetHeight
                ?? (MapPoiBottomSheet.HeightRequest > 0 ? MapPoiBottomSheet.HeightRequest : GetMapPoiSheetDefaultHeight());
        }

        return null;
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        ZoomMap(0.6);
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        ZoomMap(1.6);
    }

    private void ZoomMap(double factor)
    {
        if (MainMap == null)
            return;

        var visible = MainMap.VisibleRegion ?? _viewModel.MapSpan;
        if (visible == null)
            return;

        var nextLat = Math.Clamp(visible.LatitudeDegrees * factor, 0.0006, 120);
        var nextLon = Math.Clamp(visible.LongitudeDegrees * factor, 0.0006, 120);
        _viewModel.MapSpan = new MapSpan(visible.Center, nextLat, nextLon);
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
                    _nativeMap.UiSettings.ZoomControlsEnabled = false;
        }
    }

    private void OnNativeMarkerClick(object? sender, GoogleMap.MarkerClickEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Marker?.Id) && _nativePoiMarkers.TryGetValue(e.Marker.Id, out var poi))
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
            var candidate = imageSource.Trim();

            if (candidate.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = candidate.IndexOf(',');
                if (commaIndex > 0)
                {
                    var base64 = candidate[(commaIndex + 1)..];
                    var bytes = Convert.FromBase64String(base64);
                    return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                }
            }

            if (candidate.StartsWith("/", StringComparison.Ordinal) && !candidate.Contains("://", StringComparison.Ordinal))
            {
                candidate = candidate[1..];
            }

            var directUriBitmap = await TryLoadBitmapFromUriAsync(candidate);
            if (directUriBitmap != null)
                return directUriBitmap;

            if (File.Exists(candidate))
            {
                return BitmapFactory.DecodeFile(candidate);
            }

            var appDataPath = System.IO.Path.Combine(
                FileSystem.AppDataDirectory,
                candidate.Replace("/", System.IO.Path.DirectorySeparatorChar.ToString()));
            if (File.Exists(appDataPath))
            {
                return BitmapFactory.DecodeFile(appDataPath);
            }

            var normalized = ZoneGuide.Mobile.Services.ApiService.NormalizeMediaUrl(candidate);
            var normalizedUriBitmap = await TryLoadBitmapFromUriAsync(normalized);
            if (normalizedUriBitmap != null)
                return normalizedUriBitmap;

            if (File.Exists(normalized))
            {
                return BitmapFactory.DecodeFile(normalized);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] LoadBitmapForMarkerAsync failed for {imageSource}: {ex.Message}");
        }

        return null;
    }

    private static async Task<Bitmap?> TryLoadBitmapFromUriAsync(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            var bytes = await MarkerImageHttpClient.GetByteArrayAsync(uri);
            return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
        }

        if (uri.IsFile && File.Exists(uri.LocalPath))
        {
            return BitmapFactory.DecodeFile(uri.LocalPath);
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

            UpdateTourRecenterButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateMapRegion Error: {ex}");
        }
    }

    private void OnMainMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Microsoft.Maui.Controls.Maps.Map.VisibleRegion))
        {
            UpdateTourRecenterButtonVisibility();
        }
    }

    private void UpdateTourRecenterButtonVisibility()
    {
        if (TourRecenterButton == null || MainMap == null)
            return;

        if (!TryGetTourOffscreenIndicatorPlacement(out var indicatorX, out var indicatorY, out var rotation))
        {
            TourRecenterButton.IsVisible = false;
            return;
        }

        TourRecenterButton.TranslationX = indicatorX;
        TourRecenterButton.TranslationY = indicatorY;
        TourRecenterButton.Rotation = rotation;
        TourRecenterButton.IsVisible = true;
    }

    private bool TryGetTourOffscreenIndicatorPlacement(out double x, out double y, out double rotation)
    {
        x = 0;
        y = 0;
        rotation = 0;

        if (!_viewModel.IsTourModeActive || _viewModel.UserLocation == null || MainMap == null)
            return false;

        var visibleRegion = MainMap.VisibleRegion;
        if (visibleRegion == null)
            return false;

        var halfLat = Math.Abs(visibleRegion.LatitudeDegrees) / 2d;
        var halfLon = Math.Abs(visibleRegion.LongitudeDegrees) / 2d;
        if (halfLat <= 0 || halfLon <= 0)
            return false;

        var user = _viewModel.UserLocation;
        if (user == null)
            return false;

        var dx = (user.Longitude - visibleRegion.Center.Longitude) / halfLon;
        var dy = (visibleRegion.Center.Latitude - user.Latitude) / halfLat;

        if (Math.Abs(dx) <= OffscreenSafeViewportRatio && Math.Abs(dy) <= OffscreenSafeViewportRatio)
            return false;

        var scale = Math.Max(Math.Abs(dx) / OffscreenSafeViewportRatio, Math.Abs(dy) / OffscreenSafeViewportRatio);
        if (scale <= 0)
            return false;

        var edgeDx = dx / scale;
        var edgeDy = dy / scale;

        var mapWidth = MainMap.Width > 0 ? MainMap.Width : Width;
        var mapHeight = MainMap.Height > 0 ? MainMap.Height : Height;

        if (mapWidth <= 0 || mapHeight <= 0)
            return false;

        var bottomInset = ResolveActiveBottomSheetHeight() ?? 0;

        var usableHeight = Math.Max(mapHeight - bottomInset, OffscreenIndicatorSize + (OffscreenIndicatorEdgePadding * 2));

        var normalizedX = (edgeDx / OffscreenSafeViewportRatio + 1d) / 2d;
        var normalizedY = (edgeDy / OffscreenSafeViewportRatio + 1d) / 2d;

        var targetX = normalizedX * (mapWidth - OffscreenIndicatorSize);
        var targetY = normalizedY * (usableHeight - OffscreenIndicatorSize);

        x = Math.Clamp(targetX, OffscreenIndicatorEdgePadding, mapWidth - OffscreenIndicatorSize - OffscreenIndicatorEdgePadding);
        y = Math.Clamp(targetY, OffscreenIndicatorEdgePadding, usableHeight - OffscreenIndicatorSize - OffscreenIndicatorEdgePadding);
        rotation = Math.Atan2(edgeDy, edgeDx) * 180d / Math.PI;
        return true;
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

    private void ResetMapPoiSheetLayout()
    {
        if (MapPoiBottomSheet == null)
            return;

        if (_viewModel.IsTourModeActive)
            return;

        ApplyMapPoiSheetState(MapPoiSheetHandleOnlyHeight, animate: false);
    }

    private double GetMapPoiSheetDefaultHeight()
    {
        var pageHeight = Height > 0 ? Height : 720;
        return Math.Max(280, pageHeight * MapPoiSheetDefaultRatio);
    }

    private double GetMapPoiSheetExpandedHeight()
    {
        var pageHeight = Height > 0 ? Height : 720;
        return Math.Max(GetMapPoiSheetDefaultHeight(), pageHeight * MapPoiSheetExpandedRatio);
    }

    private static double ResolveNearestMapPoiSheetSnapHeight(double currentHeight, double defaultHeight, double expandedHeight)
    {
        var candidates = new[] { MapPoiSheetHandleOnlyHeight, defaultHeight, expandedHeight };
        return candidates.OrderBy(x => Math.Abs(x - currentHeight)).First();
    }

    private void UpdateMapPoiSheetContentVisibility(double currentHeight)
    {
        if (MapPoiResults == null)
            return;

        MapPoiResults.IsVisible = currentHeight > (MapPoiSheetHandleOnlyHeight + 24);
        if (!MapPoiResults.IsVisible)
        {
            TopMapSearchBar?.Unfocus();
        }
    }

    private async Task OpenMapPoiSheetAsync(bool focusSearch)
    {
        if (_viewModel.IsTourModeActive)
            return;

        var targetHeight = GetMapPoiSheetDefaultHeight();
        ApplyMapPoiSheetState(targetHeight, animate: true);

        if (!focusSearch)
            return;

        await Task.Delay(120);
        TopMapSearchBar?.Focus();
    }

    private async Task OpenTourSearchOverlayAsync()
    {
        if (TourSearchOverlay == null)
            return;

        TourSearchOverlay.IsVisible = true;

        ExecutePerformSearchCommand();

        await Task.Delay(120);
        TourOverlaySearchBar?.Focus();
    }

    private void CloseTourSearchOverlay()
    {
        if (TourSearchOverlay == null)
            return;

        TourSearchOverlay.IsVisible = false;
        TourOverlaySearchBar?.Unfocus();

        if (TourSearchResults != null)
        {
            TourSearchResults.SelectedItem = null;
        }
    }

    private async void OnTopMapSearchFocused(object? sender, FocusEventArgs e)
    {
        await OpenMapPoiSheetAsync(focusSearch: false);
    }

    private async void OnTopMapSearchSubmitted(object? sender, EventArgs e)
    {
        await OpenMapPoiSheetAsync(focusSearch: false);
    }

    private void OnMapPoiResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not POI poi)
            return;

        _viewModel.SelectPOICommand.Execute(poi);
        if (ReferenceEquals(sender, TourSearchResults))
        {
            CloseTourSearchOverlay();
        }
        else
        {
            TopMapSearchBar?.Unfocus();
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }
    }

    private async void OnTourSearchTapped(object? sender, EventArgs e)
    {
        if (TourSearchOverlay?.IsVisible == true)
        {
            CloseTourSearchOverlay();
            return;
        }

        await OpenTourSearchOverlayAsync();
    }

    private void OnTourSearchBackdropTapped(object? sender, EventArgs e)
    {
        CloseTourSearchOverlay();
    }

    private async void OnTourSearchSubmitted(object? sender, EventArgs e)
    {
        ExecutePerformSearchCommand();
        await Task.CompletedTask;
    }

    private void ExecutePerformSearchCommand()
    {
        var command = _viewModel.PerformSearchCommand;
        if (!command.CanExecute(null))
            return;

        if (command is IAsyncRelayCommand asyncCommand)
        {
            _ = asyncCommand.ExecuteAsync(null);
            return;
        }

        command.Execute(null);
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        await QrScannerNavigationHelper.OpenScannerAsync(this);
    }
}
