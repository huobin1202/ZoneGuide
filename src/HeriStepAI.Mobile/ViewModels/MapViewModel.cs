using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;

namespace HeriStepAI.Mobile.ViewModels;

/// <summary>
/// ViewModel cho Map View
/// </summary>
public partial class MapViewModel : ObservableObject
{
    private readonly ILocationService _locationService;
    private readonly IGeofenceService _geofenceService;
    private readonly IPOIRepository _poiRepository;
    private readonly INarrationService _narrationService;

    [ObservableProperty]
    private Location? userLocation;

    [ObservableProperty]
    private MapSpan? mapSpan;

    [ObservableProperty]
    private POI? selectedPOI;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<POI> POIs { get; } = new();
    public ObservableCollection<Pin> MapPins { get; } = new();

    public MapViewModel(
        ILocationService locationService,
        IGeofenceService geofenceService,
        IPOIRepository poiRepository,
        INarrationService narrationService)
    {
        _locationService = locationService;
        _geofenceService = geofenceService;
        _poiRepository = poiRepository;
        _narrationService = narrationService;

        _locationService.LocationChanged += OnLocationChanged;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        
        try
        {
            // Lấy vị trí hiện tại
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                UserLocation = new Location(location.Latitude, location.Longitude);
                MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
            }
            else
            {
                // Default location (Hồ Chí Minh)
                UserLocation = new Location(10.8231, 106.6297);
                MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(1));
            }

            // Tải POIs
            await LoadPOIsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadPOIsAsync()
    {
        try
        {
            var pois = await _poiRepository.GetActiveAsync();
            
            POIs.Clear();
            MapPins.Clear();

            foreach (var poi in pois)
            {
                POIs.Add(poi);
                
                var pin = new Pin
                {
                    Label = poi.Name,
                    Address = poi.ShortDescription,
                    Location = new Location(poi.Latitude, poi.Longitude),
                    Type = PinType.Place
                };
                
                MapPins.Add(pin);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CenterOnUser()
    {
        if (UserLocation != null)
        {
            MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
        }
        else
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                UserLocation = new Location(location.Latitude, location.Longitude);
                MapSpan = MapSpan.FromCenterAndRadius(UserLocation, Distance.FromKilometers(0.5));
            }
        }
    }

    [RelayCommand]
    private void SelectPOI(POI poi)
    {
        SelectedPOI = poi;
        MapSpan = MapSpan.FromCenterAndRadius(
            new Location(poi.Latitude, poi.Longitude), 
            Distance.FromMeters(200));
    }

    [RelayCommand]
    private async Task PlaySelectedPOI()
    {
        if (SelectedPOI == null)
            return;

        var item = new NarrationQueueItem
        {
            POI = SelectedPOI,
            TTSText = SelectedPOI.TTSScript ?? SelectedPOI.FullDescription,
            Language = SelectedPOI.Language,
            Priority = SelectedPOI.Priority,
            TriggerType = GeofenceEventType.Enter,
            TriggerDistance = 0
        };

        await _narrationService.PlayImmediatelyAsync(item);
    }

    [RelayCommand]
    private async Task NavigateToPOI()
    {
        if (SelectedPOI == null)
            return;

        var location = new Location(SelectedPOI.Latitude, SelectedPOI.Longitude);
        var options = new MapLaunchOptions { NavigationMode = NavigationMode.Walking };

        await Map.Default.OpenAsync(location, options);
    }

    [RelayCommand]
    private async Task ViewPOIDetail()
    {
        if (SelectedPOI == null)
            return;

        await Shell.Current.GoToAsync($"POIDetailPage?id={SelectedPOI.Id}");
    }

    private void OnLocationChanged(object? sender, LocationData location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UserLocation = new Location(location.Latitude, location.Longitude);
        });
    }

    public POI? FindNearestPOI()
    {
        return _geofenceService.NearestPOI;
    }

    public double? GetDistanceToNearestPOI()
    {
        return _geofenceService.NearestPOIDistance;
    }
}
