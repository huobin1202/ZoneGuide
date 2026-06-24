using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

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
    private bool _hasInitialized;
    private bool _mapReady;
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
        else if (e.PropertyName == nameof(MapViewModel.SelectedPOI))
        {
            if (_viewModel.SelectedPOI == null)
                FitMapToAllPins();
            else
                HighlightPoiOnMap(_viewModel.SelectedPOI);
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
                UpdateUserLocationOnMap();
            });
        }
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

        // Setup POI click callback from JS
        await SetupPoiClickCallbackAsync();

        try
        {
            if (!_hasInitialized)
            {
                await _viewModel.InitializeAsync();
                _hasInitialized = true;
            }
            else
            {
                await _viewModel.RefreshVisibleDataAsync(syncFirst: _viewModel.POIs.Count == 0);
                await _viewModel.ApplyTourRequestAsync();
            }

            if (!_autoPlayRequestedOnNavigation)
            {
                await _viewModel.SuppressAutoNarrationForCurrentInRangePoisAsync();
            }

            await RefreshMapAsync();

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

            // Give WebView time to fully render
            await Task.Delay(500);
            await RefreshMapAsync();

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

    private async Task SetupPoiClickCallbackAsync()
    {
        try
        {
            // Remove old callback if any
            await MapWebView.EvaluateJavaScriptAsync("window._poiClickCallback = null;");
            // Set new callback
            var js = "window._poiClickCallback = function(poiId) { " +
                "if (window._dotNetPoiClickRef) { " +
                "window._dotNetPoiClickRef.invokeMethodAsync('OnPoiMarkerClicked', poiId); " +
                "} };";
            await MapWebView.EvaluateJavaScriptAsync(js);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] SetupPoiClickCallback error: {ex.Message}");
        }
    }

    public void OnPoiMarkerClicked(int poiId)
    {
        var poi = _viewModel.POIs.FirstOrDefault(p => p.Id == poiId);
        if (poi != null && _viewModel.SelectPOICommand.CanExecute(poi))
        {
            _viewModel.SelectPOICommand.Execute(poi);
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

                        await RefreshMapAsync();
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

                await RefreshMapAsync();
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

    private void OnPoisCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DebounceCollectionUpdate(ref _pinsUpdateDebounceCts, () => _ = UpdatePoisOnMapAsync());
    }

    private void OnTourRoutePointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DebounceCollectionUpdate(ref _routeUpdateDebounceCts, () => _ = UpdateRouteOnMapAsync());
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

    private async Task RefreshMapAsync()
    {
        if (!_mapReady) return;
        await UpdatePoisOnMapAsync();
        await UpdateRouteOnMapAsync();
        await UpdateUserLocationOnMap();
        UpdateMapRegion();
        UpdateTourRecenterButtonVisibility();
    }

    private async Task UpdatePoisOnMapAsync()
    {
        try
        {
            var pois = _viewModel.POIs;
            if (pois == null || pois.Count == 0)
            {
                await MapWebView.EvaluateJavaScriptAsync("zoneGuideMap.updatePois([], false);");
                return;
            }

            var poiList = pois.Select(p => new
            {
                id = p.Id,
                name = p.Name ?? "",
                lat = p.Latitude,
                lng = p.Longitude,
                order = _viewModel.IsTourModeActive ? p.OrderInTour : 0
            }).ToList();

            var json = JsonSerializer.Serialize(poiList);
            var escapedJson = json.Replace("'", "\\'");
            var isTour = _viewModel.IsTourModeActive ? "true" : "false";
            await MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.updatePois('{escapedJson}', {isTour});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdatePoisOnMap error: {ex.Message}");
        }
    }

    private async Task UpdateRouteOnMapAsync()
    {
        try
        {
            var routePoints = _viewModel.TourRoutePoints;
            if (routePoints == null || routePoints.Count < 2)
            {
                await MapWebView.EvaluateJavaScriptAsync("zoneGuideMap.clearRoute();");
                return;
            }

            var points = routePoints.Select(p => new { lat = p.Latitude, lng = p.Longitude }).ToList();
            var json = JsonSerializer.Serialize(points);
            var escapedJson = json.Replace("'", "\\'");
            await MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.updateRoute('{escapedJson}');");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateRouteOnMap error: {ex.Message}");
        }
    }

    private async Task UpdateUserLocationOnMap()
    {
        try
        {
            if (_viewModel.UserLocation != null)
            {
                await MapWebView.EvaluateJavaScriptAsync(
                    $"zoneGuideMap.updateUserLocation({_viewModel.UserLocation.Latitude}, {_viewModel.UserLocation.Longitude});");
            }
            else
            {
                await MapWebView.EvaluateJavaScriptAsync("zoneGuideMap.clearUserLocation();");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateUserLocation error: {ex.Message}");
        }
    }

    private async Task HighlightPoiOnMap(POI? poi)
    {
        if (poi == null) return;
        try
        {
            await MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.highlightPoi({poi.Id});");
            await MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.setCenter({poi.Latitude}, {poi.Longitude}, 15);");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] HighlightPoi error: {ex.Message}");
        }
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

    private async void OnZoomInClicked(object? sender, EventArgs e)
    {
        try
        {
            await MapWebView.EvaluateJavaScriptAsync(
                "var z = map.getZoom(); map.setZoom(Math.min(z + 1, 19));");
        }
        catch { }
    }

    private async void OnZoomOutClicked(object? sender, EventArgs e)
    {
        try
        {
            await MapWebView.EvaluateJavaScriptAsync(
                "var z = map.getZoom(); map.setZoom(Math.max(z - 1, 2));");
        }
        catch { }
    }

    #region WebView lifecycle

    private void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // Could show loading indicator
    }

    private async void OnMapWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _mapReady = true;
        System.Diagnostics.Debug.WriteLine("[MapPage] Leaflet map WebView navigated");

        try
        {
            // Verify map is ready
            var result = await MapWebView.EvaluateJavaScriptAsync("zoneGuideMap.isMapReady()");
            System.Diagnostics.Debug.WriteLine($"[MapPage] Map ready check: {result}");

            await RefreshMapAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] WebView init error: {ex.Message}");
        }
    }

    #endregion

    private void UpdateMapRegion()
    {
        try
        {
            if (_viewModel.MapSpan == null || !_mapReady)
                return;

            var center = _viewModel.MapSpan.Center;
            var zoom = 15;
            _ = MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.setCenter({center.Latitude}, {center.Longitude}, {zoom});");
            UpdateTourRecenterButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateMapRegion Error: {ex}");
        }
    }

    private void UpdateTourRecenterButtonVisibility()
    {
        if (TourRecenterButton == null)
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

        if (!_viewModel.IsTourModeActive || _viewModel.UserLocation == null || !_mapReady)
            return false;

        // Simplified - we can't easily get visible region from WebView synchronously,
        // so we skip the offscreen indicator for now.
        return false;
    }

    private void FitMapToAllPins()
    {
        try
        {
            var pois = _viewModel.POIs;
            if (pois == null || pois.Count == 0 || !_mapReady)
                return;

            var poiList = pois.Select(p => new { lat = p.Latitude, lng = p.Longitude }).ToList();
            if (poiList.Count == 0) return;

            var json = JsonSerializer.Serialize(poiList);
            var escapedJson = json.Replace("'", "\\'");
            _ = MapWebView.EvaluateJavaScriptAsync($"zoneGuideMap.fitBounds('{escapedJson}');");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] FitMapToAllPins Error: {ex}");
        }
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
