using System.Collections.Specialized;
using System.ComponentModel;
using ZoneGuide.Shared.Models;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    private const double SheetExpandedHeight = 306;
    private const double SheetCollapsedHeight = 128;
    private const uint SheetSnapAnimationMs = 460;

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
                UpdateSheetContentVisibility(nextHeight);
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
            UpdateSheetContentVisibility(targetHeight);

            return;
        }

        this.AbortAnimation("PoiBottomSheetSnap");
        var animation = new Animation(v =>
        {
            PoiBottomSheet.HeightRequest = v;
            UpdateSheetContentVisibility(v);
        }, startHeight, targetHeight);

        animation.Commit(this, "PoiBottomSheetSnap", 16, SheetSnapAnimationMs, Easing.SinOut);
    }

    private void UpdateSheetContentVisibility(double currentHeight)
    {
        if (PoiSheetContent == null)
            return;

        PoiSheetContent.IsVisible = currentHeight > (SheetCollapsedHeight + 8);
    }

    private async void OnMiniMapTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        await Shell.Current.GoToAsync("//map");
    }

    private async void OnMapButtonTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        await Shell.Current.GoToAsync("//map");
    }

    private async void OnShowStartTapped(object? sender, EventArgs e)
    {
        var command = _viewModel.StartTourCommand;
        if (command == null || !command.CanExecute(null))
            return;

        await command.ExecuteAsync(null);
    }

    private async void OnSearchTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map?openSearch=true");
    }

    private async void OnDirectionsTapped(object? sender, EventArgs e)
    {
        if (_viewModel.Tour == null)
            return;

        var command = _viewModel.StartTourCommand;
        if (command == null || !command.CanExecute(null))
            return;

        await command.ExecuteAsync(null);
    }

    private async void RenderMiniMap()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(RenderMiniMap);
            return;
        }

        if (MiniRouteMap == null)
            return;

        var orderedPois = _viewModel.POIs
            .Where(IsValidCoordinate)
            .OrderBy(p => p.OrderInTour <= 0 ? int.MaxValue : p.OrderInTour)
            .ThenBy(p => p.Name)
            .ToList();

        if (orderedPois.Count == 0)
            return;

        try
        {
            var points = string.Join(",", orderedPois.Select(p => $"[{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}]"));
            var js = $"if (typeof initializeMiniRouteMap !== 'undefined') initializeMiniRouteMap([{points}]);";
            await MiniRouteMap.EvaluateJavaScriptAsync(js);
        }
        catch { }
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
