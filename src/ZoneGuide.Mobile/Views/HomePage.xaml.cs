using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using ZoneGuide.Mobile.Services;
using ZoneGuide.Mobile.ViewModels;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly ApiService _apiService = new();

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
        
        // Gửi heartbeat khởi động để hiển thị trên mobile monitoring
        await SendStartupHeartbeatAsync();
    }

    private async Task SendStartupHeartbeatAsync()
    {
        try
        {
            await _apiService.UploadMobileHeartbeatAsync(new MobileLiveHeartbeatDto
            {
                SessionId = Guid.NewGuid().ToString(),
                DeviceId = $"{DeviceInfo.Current.Model}_{DeviceInfo.Current.Platform}",
                IsTracking = false,
                Platform = DeviceInfo.Current.Platform.ToString(),
                AppVersion = AppInfo.Current.VersionString,
                PreferredLanguage = "vi",
                Latitude = 0,
                Longitude = 0,
                StatusMessage = "App started - home screen"
            });
        }
        catch
        {
            // Ignore heartbeat errors
        }
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        await QrScannerNavigationHelper.OpenScannerAsync(this);
    }
}
