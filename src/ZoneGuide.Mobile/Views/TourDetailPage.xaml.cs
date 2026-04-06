using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ZoneGuide.Shared.Models;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    private const double SheetExpandedHeight = 348;
    private const double SheetCollapsedHeight = 128;
    private const uint SheetSnapAnimationMs = 260;

    private double _panStartHeight;
    private bool _isPanning;
    private readonly TourDetailViewModel _viewModel;

    public TourDetailPage(TourDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.POIs.CollectionChanged += OnPoisChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        ApplySheetState(SheetExpandedHeight, animate: false);
        RenderMiniMap();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TourDetailViewModel.Tour))
        {
            RenderMiniMap();
        }
    }

    private void OnPoisChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderMiniMap();
    }

    private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (PoiBottomSheet == null)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isPanning = true;
                _panStartHeight = PoiBottomSheet.HeightRequest > 0
                    ? PoiBottomSheet.HeightRequest
                    : SheetExpandedHeight;
                break;

            case GestureStatus.Running:
                if (!_isPanning)
                    return;

                var nextHeight = _panStartHeight - e.TotalY;
                if (nextHeight < SheetCollapsedHeight)
                    nextHeight = SheetCollapsedHeight;
                if (nextHeight > SheetExpandedHeight)
                    nextHeight = SheetExpandedHeight;

                PoiBottomSheet.HeightRequest = nextHeight;
                if (PoiCollectionView != null)
                {
                    PoiCollectionView.IsVisible = nextHeight > (SheetCollapsedHeight + 16);
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_isPanning)
                    return;

                _isPanning = false;

                var snapHeight = GetNearestSnapHeight(PoiBottomSheet.HeightRequest, e.TotalY);
                ApplySheetState(snapHeight, animate: true);
                break;
        }
    }

    private static double GetNearestSnapHeight(double currentHeight, double totalPanY)
    {
        var midpoint = (SheetExpandedHeight + SheetCollapsedHeight) / 2d;

        if (Math.Abs(totalPanY) > 12)
        {
            return totalPanY > 0 ? SheetCollapsedHeight : SheetExpandedHeight;
        }

        return currentHeight >= midpoint ? SheetExpandedHeight : SheetCollapsedHeight;
    }

    private void ApplySheetState(double targetHeight, bool animate)
    {
        if (PoiBottomSheet == null)
            return;

        var startHeight = PoiBottomSheet.HeightRequest > 0
            ? PoiBottomSheet.HeightRequest
            : targetHeight;

        if (!animate || Math.Abs(startHeight - targetHeight) < 0.5d)
        {
            PoiBottomSheet.HeightRequest = targetHeight;
            if (PoiCollectionView != null)
            {
                PoiCollectionView.IsVisible = targetHeight > (SheetCollapsedHeight + 8);
            }

            return;
        }

        this.AbortAnimation("PoiBottomSheetSnap");
        var animation = new Animation(v =>
        {
            PoiBottomSheet.HeightRequest = v;

            if (PoiCollectionView != null)
            {
                PoiCollectionView.IsVisible = v > (SheetCollapsedHeight + 8);
            }
        }, startHeight, targetHeight);

        animation.Commit(this, "PoiBottomSheetSnap", 16, SheetSnapAnimationMs, Easing.CubicOut);
    }

    private async void OnMiniMapTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        await Shell.Current.GoToAsync($"//map?tourId={_viewModel.Tour.Id}&startTour=true");
    }

    private async void OnMapButtonTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        await Shell.Current.GoToAsync($"//map?tourId={_viewModel.Tour.Id}");
    }

    private async void OnDirectionsTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        await Shell.Current.GoToAsync($"//map?tourId={_viewModel.Tour.Id}&startTour=true");
    }

    private void RenderMiniMap()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(RenderMiniMap);
            return;
        }

        if (MiniRouteMap == null)
            return;

        MiniRouteMap.Pins.Clear();
        MiniRouteMap.MapElements.Clear();

        var orderedPois = _viewModel.POIs
            .Where(IsValidCoordinate)
            .OrderBy(p => p.OrderInTour <= 0 ? int.MaxValue : p.OrderInTour)
            .ThenBy(p => p.Name)
            .ToList();

        if (orderedPois.Count == 0)
            return;

        var route = new Polyline
        {
            StrokeColor = Color.FromArgb("#2563EB"),
            StrokeWidth = 4
        };

        foreach (var poi in orderedPois)
        {
            var location = new Location(poi.Latitude, poi.Longitude);
            route.Geopath.Add(location);

            var orderLabel = poi.OrderInTour > 0 ? poi.OrderInTour.ToString() : "?";
            MiniRouteMap.Pins.Add(new Pin
            {
                Label = $"{orderLabel}. {poi.Name}",
                Location = location,
                Type = PinType.Place
            });
        }

        if (route.Geopath.Count > 1)
        {
            MiniRouteMap.MapElements.Add(route);
        }

        FitMiniMapRegion(orderedPois);
    }

    private void FitMiniMapRegion(IReadOnlyList<POI> pois)
    {
        if (MiniRouteMap == null || pois.Count == 0)
            return;

        if (pois.Count == 1)
        {
            var one = pois[0];
            MiniRouteMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(one.Latitude, one.Longitude),
                Distance.FromKilometers(0.7)));
            return;
        }

        var minLat = pois.Min(p => p.Latitude);
        var maxLat = pois.Max(p => p.Latitude);
        var minLon = pois.Min(p => p.Longitude);
        var maxLon = pois.Max(p => p.Longitude);

        var centerLat = (minLat + maxLat) / 2d;
        var centerLon = (minLon + maxLon) / 2d;

        var latKm = (maxLat - minLat) * 111d;
        var lonKm = (maxLon - minLon) * 111d * Math.Cos(centerLat * Math.PI / 180d);
        var radiusKm = Math.Clamp((Math.Max(latKm, lonKm) * 0.75d) + 0.35d, 0.6d, 20d);

        MiniRouteMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(centerLat, centerLon),
            Distance.FromKilometers(radiusKm)));
    }

    private static bool IsValidCoordinate(POI poi)
    {
        return poi.Latitude is >= -90 and <= 90 && poi.Longitude is >= -180 and <= 180;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await Shell.Current.GoToAsync("//tours");
    }
}
