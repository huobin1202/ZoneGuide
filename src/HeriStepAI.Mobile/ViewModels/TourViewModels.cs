using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using System.Collections.ObjectModel;

namespace HeriStepAI.Mobile.ViewModels;

/// <summary>
/// ViewModel cho danh sách Tour
/// </summary>
public partial class TourListViewModel : ObservableObject
{
    private readonly ITourRepository _tourRepository;
    private readonly ISyncService _syncService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    public ObservableCollection<Tour> Tours { get; } = new();

    public TourListViewModel(
        ITourRepository tourRepository,
        ISyncService syncService)
    {
        _tourRepository = tourRepository;
        _syncService = syncService;
    }

    public async Task InitializeAsync()
    {
        await LoadToursAsync();
    }

    [RelayCommand]
    private async Task LoadToursAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var tours = await _tourRepository.GetActiveAsync();
            
            Tours.Clear();
            foreach (var tour in tours)
            {
                Tours.Add(tour);
            }
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadToursAsync();
    }

    [RelayCommand]
    private async Task ViewDetail(Tour tour)
    {
        await Shell.Current.GoToAsync($"TourDetailPage?id={tour.Id}");
    }
}

/// <summary>
/// ViewModel chi tiết Tour
/// </summary>
[QueryProperty(nameof(TourId), "id")]
public partial class TourDetailViewModel : ObservableObject
{
    private readonly ITourRepository _tourRepository;
    private readonly IPOIRepository _poiRepository;
    private readonly ISyncService _syncService;
    private readonly IGeofenceService _geofenceService;

    [ObservableProperty]
    private int tourId;

    [ObservableProperty]
    private Tour? tour;

    [ObservableProperty]
    private bool isOfflineAvailable;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private double downloadProgress;

    public ObservableCollection<POI> POIs { get; } = new();

    public TourDetailViewModel(
        ITourRepository tourRepository,
        IPOIRepository poiRepository,
        ISyncService syncService,
        IGeofenceService geofenceService)
    {
        _tourRepository = tourRepository;
        _poiRepository = poiRepository;
        _syncService = syncService;
        _geofenceService = geofenceService;
    }

    async partial void OnTourIdChanged(int value)
    {
        await LoadTourAsync();
    }

    private async Task LoadTourAsync()
    {
        Tour = await _tourRepository.GetByIdAsync(TourId);
        
        if (Tour != null)
        {
            IsOfflineAvailable = await _syncService.IsTourOfflineAvailableAsync(TourId);
            
            var pois = await _poiRepository.GetByTourIdAsync(TourId);
            POIs.Clear();
            foreach (var poi in pois)
            {
                POIs.Add(poi);
            }
        }
    }

    [RelayCommand]
    private async Task StartTourAsync()
    {
        if (Tour == null || POIs.Count == 0)
            return;

        // Thêm tất cả POI của tour vào geofence
        _geofenceService.ClearPOIs();
        _geofenceService.AddPOIs(POIs);

        // Chuyển sang trang Map
        await Shell.Current.GoToAsync("//map");
    }

    [RelayCommand]
    private async Task ToggleOfflineAsync()
    {
        if (IsOfflineAvailable)
            await DeleteOfflineAsync();
        else
            await DownloadOfflineAsync();
    }

    [RelayCommand]
    private async Task DownloadOfflineAsync()
    {
        if (IsDownloading)
            return;

        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            var success = await _syncService.DownloadTourOfflineAsync(TourId);
            IsOfflineAvailable = success;

            if (success)
            {
                await Shell.Current.DisplayAlert("Thành công", "Đã tải nội dung offline", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Lỗi", "Không thể tải nội dung offline", "OK");
            }
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteOfflineAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Xác nhận", 
            "Bạn có chắc muốn xóa nội dung offline?", 
            "Xóa", 
            "Hủy");

        if (confirm)
        {
            var success = await _syncService.DeleteTourOfflineAsync(TourId);
            IsOfflineAvailable = !success;
        }
    }

    [RelayCommand]
    private async Task ViewPOIDetail(POI poi)
    {
        await Shell.Current.GoToAsync($"POIDetailPage?id={poi.Id}");
    }
}
