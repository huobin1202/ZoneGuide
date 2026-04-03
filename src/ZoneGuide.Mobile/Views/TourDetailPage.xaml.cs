using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using ZoneGuide.Shared.Models;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    private const double SheetExpandedHeight = 350;
    private const double SheetHalfHeight = 220;
    private const double SheetCollapsedHeight = 108;

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

        ApplySheetState(SheetHalfHeight);
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

                var snapHeight = GetNearestSnapHeight(PoiBottomSheet.HeightRequest);
                ApplySheetState(snapHeight);
                break;
        }
    }

    private static double GetNearestSnapHeight(double currentHeight)
    {
        var targets = new[]
        {
            SheetCollapsedHeight,
            SheetHalfHeight,
            SheetExpandedHeight
        };

        var nearest = targets[0];
        var bestDistance = Math.Abs(currentHeight - nearest);

        for (var i = 1; i < targets.Length; i++)
        {
            var candidate = targets[i];
            var distance = Math.Abs(currentHeight - candidate);
            if (distance < bestDistance)
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    private void ApplySheetState(double targetHeight)
    {
        if (PoiBottomSheet != null)
        {
            PoiBottomSheet.HeightRequest = targetHeight;
        }

        if (PoiCollectionView != null)
        {
            PoiCollectionView.IsVisible = targetHeight > (SheetCollapsedHeight + 8);
        }
    }

    private async void OnMiniMapTapped(object? sender, EventArgs e)
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
