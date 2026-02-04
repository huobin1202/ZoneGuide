using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using System.Diagnostics;

namespace HeriStepAI.Mobile.Services;

/// <summary>
/// Service theo dõi GPS thời gian thực
/// </summary>
public class LocationService : ILocationService, IDisposable
{
    public event EventHandler<LocationData>? LocationChanged;
    public event EventHandler<string>? LocationError;

    public LocationData? CurrentLocation { get; private set; }
    public bool IsTracking { get; private set; }

    private CancellationTokenSource? _cancellationTokenSource;
    private GPSAccuracyLevel _accuracyLevel = GPSAccuracyLevel.Medium;

    public async Task<bool> StartTrackingAsync(GPSAccuracyLevel accuracy = GPSAccuracyLevel.Medium)
    {
        if (IsTracking)
            return true;

        var hasPermission = await RequestPermissionAsync();
        if (!hasPermission)
        {
            LocationError?.Invoke(this, "Không có quyền truy cập vị trí");
            return false;
        }

        _accuracyLevel = accuracy;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var request = CreateGeolocationRequest(accuracy);

            IsTracking = true;

            // Bắt đầu theo dõi liên tục
            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested && IsTracking)
                {
                    try
                    {
                        var location = await Geolocation.GetLocationAsync(request, _cancellationTokenSource.Token);
                        
                        if (location != null)
                        {
                            CurrentLocation = new LocationData
                            {
                                Latitude = location.Latitude,
                                Longitude = location.Longitude,
                                Accuracy = location.Accuracy ?? 0,
                                Altitude = location.Altitude,
                                Speed = location.Speed,
                                Heading = location.Course,
                                Timestamp = location.Timestamp.UtcDateTime
                            };

                            LocationChanged?.Invoke(this, CurrentLocation);
                        }

                        // Delay dựa trên mức độ chính xác
                        var delay = accuracy switch
                        {
                            GPSAccuracyLevel.Low => 30000,    // 30 giây
                            GPSAccuracyLevel.Medium => 10000, // 10 giây
                            GPSAccuracyLevel.High => 3000,    // 3 giây
                            _ => 10000
                        };

                        await Task.Delay(delay, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Location error: {ex.Message}");
                        LocationError?.Invoke(this, ex.Message);
                        await Task.Delay(5000); // Chờ 5 giây trước khi thử lại
                    }
                }
            }, _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            IsTracking = false;
            LocationError?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task StopTrackingAsync()
    {
        IsTracking = false;
        
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public async Task<LocationData?> GetCurrentLocationAsync()
    {
        try
        {
            var hasPermission = await RequestPermissionAsync();
            if (!hasPermission)
                return null;

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var location = await Geolocation.GetLocationAsync(request);

            if (location != null)
            {
                CurrentLocation = new LocationData
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Accuracy = location.Accuracy ?? 0,
                    Altitude = location.Altitude,
                    Speed = location.Speed,
                    Heading = location.Course,
                    Timestamp = location.Timestamp.UtcDateTime
                };
                return CurrentLocation;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetCurrentLocation error: {ex.Message}");
            LocationError?.Invoke(this, ex.Message);
        }

        return null;
    }

    public async Task<bool> CheckPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        // Yêu cầu quyền background nếu cần
        if (status == PermissionStatus.Granted)
        {
            var bgStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (bgStatus != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.LocationAlways>();
            }
        }

        return status == PermissionStatus.Granted;
    }

    public void SetAccuracyLevel(GPSAccuracyLevel level)
    {
        _accuracyLevel = level;
    }

    private static GeolocationRequest CreateGeolocationRequest(GPSAccuracyLevel accuracy)
    {
        var geoAccuracy = accuracy switch
        {
            GPSAccuracyLevel.Low => GeolocationAccuracy.Low,
            GPSAccuracyLevel.Medium => GeolocationAccuracy.Medium,
            GPSAccuracyLevel.High => GeolocationAccuracy.Best,
            _ => GeolocationAccuracy.Medium
        };

        return new GeolocationRequest(geoAccuracy, TimeSpan.FromSeconds(10));
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
