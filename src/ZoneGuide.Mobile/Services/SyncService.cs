using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service đồng bộ dữ liệu
/// </summary>
public class SyncService : ISyncService
{
    public event EventHandler? SyncStarted;
    public event EventHandler<bool>? SyncCompleted;
    public event EventHandler<double>? SyncProgress;

    private readonly ApiService _apiService;
    private readonly IPOIRepository _poiRepository;
    private readonly ITourRepository _tourRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ISettingsService _settingsService;

    public bool IsSyncing { get; private set; }
    public DateTime? LastSyncTime { get; private set; }

    public SyncService(
        ApiService apiService,
        IPOIRepository poiRepository,
        ITourRepository tourRepository,
        IAnalyticsRepository analyticsRepository,
        ISettingsService settingsService)
    {
        _apiService = apiService;
        _poiRepository = poiRepository;
        _tourRepository = tourRepository;
        _analyticsRepository = analyticsRepository;
        _settingsService = settingsService;
    }

    public async Task<bool> SyncFromServerAsync()
    {
        if (IsSyncing)
            return false;

        try
        {
            IsSyncing = true;
            SyncStarted?.Invoke(this, EventArgs.Empty);

            var request = new SyncRequest
            {
                LastSyncTime = LastSyncTime,
                Language = _settingsService.Settings.PreferredLanguage
            };

            SyncProgress?.Invoke(this, 0.1);

            var syncData = await _apiService.SyncDataAsync(request);
            
            if (syncData == null)
            {
                SyncCompleted?.Invoke(this, false);
                return false;
            }

            SyncProgress?.Invoke(this, 0.3);

            // Xóa các POI đã bị xóa trên server
            foreach (var deletedId in syncData.DeletedPOIIds)
            {
                await _poiRepository.DeleteAsync(deletedId);
            }

            SyncProgress?.Invoke(this, 0.5);

            // Cập nhật POIs
            foreach (var poiDto in syncData.POIs)
            {
                var poi = MapToPOI(poiDto);
                await _poiRepository.InsertOrUpdateAsync(poi);
            }

            SyncProgress?.Invoke(this, 0.7);

            // Xóa các Tour đã bị xóa
            foreach (var deletedId in syncData.DeletedTourIds)
            {
                await _tourRepository.DeleteAsync(deletedId);
            }

            // Cập nhật Tours
            foreach (var tourDto in syncData.Tours)
            {
                var tour = MapToTour(tourDto);
                await _tourRepository.InsertOrUpdateAsync(tour);
            }

            SyncProgress?.Invoke(this, 1.0);

            LastSyncTime = syncData.LastSyncTime;
            await _settingsService.SetAsync("last_sync_time", LastSyncTime);

            SyncCompleted?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync error: {ex.Message}");
            SyncCompleted?.Invoke(this, false);
            return false;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public async Task<bool> UploadAnalyticsAsync()
    {
        try
        {
            var deviceId = await _settingsService.GetAsync<string>("anonymous_device_id");
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString("N");
                await _settingsService.SetAsync("anonymous_device_id", deviceId);
            }

            var locations = await _analyticsRepository.GetLocationsByDateRangeAsync(
                DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            var narrations = await _analyticsRepository.GetNarrationsByDateRangeAsync(
                DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

            var uploadData = new AnalyticsUploadDto
            {
                AnonymousDeviceId = deviceId,
                Locations = locations.Select(l => new LocationHistoryDto
                {
                    SessionId = l.SessionId,
                    Latitude = l.Latitude,
                    Longitude = l.Longitude,
                    Accuracy = l.Accuracy,
                    Speed = l.Speed,
                    Heading = l.Heading,
                    Altitude = l.Altitude,
                    Timestamp = l.Timestamp
                }).ToList(),
                Narrations = narrations.Select(n => new NarrationHistoryDto
                {
                    SessionId = n.SessionId,
                    POIId = n.POIId.ToString(),
                    POIName = n.POIName ?? string.Empty,
                    Language = n.Language ?? "vi-VN",
                    StartTime = n.StartTime,
                    EndTime = n.EndTime,
                    DurationSeconds = n.DurationSeconds,
                    TotalDurationSeconds = n.TotalDurationSeconds,
                    Completed = n.Completed,
                    TriggerType = n.TriggerType ?? string.Empty,
                    TriggerDistance = n.TriggerDistance,
                    TriggerLatitude = n.TriggerLatitude,
                    TriggerLongitude = n.TriggerLongitude
                }).ToList()
            };

            return await _apiService.UploadAnalyticsAsync(uploadData);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadTourOfflineAsync(int tourId)
    {
        try
        {
            var tour = await _apiService.GetTourAsync(tourId);
            if (tour?.POIs == null)
                return false;

            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());
            Directory.CreateDirectory(offlineDir);

            foreach (var poi in tour.POIs)
            {
                // Tải audio
                if (!string.IsNullOrEmpty(poi.AudioUrl))
                {
                    var audioData = await _apiService.DownloadAudioAsync(poi.AudioUrl);
                    if (audioData != null)
                    {
                        var audioPath = Path.Combine(offlineDir, $"audio_{poi.Id}.mp3");
                        await File.WriteAllBytesAsync(audioPath, audioData);
                    }
                }

                // Tải ảnh
                if (!string.IsNullOrEmpty(poi.ImageUrl))
                {
                    var imageData = await _apiService.DownloadImageAsync(poi.ImageUrl);
                    if (imageData != null)
                    {
                        var imagePath = Path.Combine(offlineDir, $"image_{poi.Id}.jpg");
                        await File.WriteAllBytesAsync(imagePath, imageData);
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> DeleteTourOfflineAsync(int tourId)
    {
        try
        {
            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());
            if (Directory.Exists(offlineDir))
            {
                Directory.Delete(offlineDir, true);
            }
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> IsTourOfflineAvailableAsync(int tourId)
    {
        var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());
        return Task.FromResult(Directory.Exists(offlineDir));
    }

    private static POI MapToPOI(POIDto dto)
    {
        return new POI
        {
            Id = int.TryParse(dto.Id, out var id) ? id : 0,
            UniqueCode = dto.UniqueCode,
            Name = dto.Name,
            ShortDescription = dto.ShortDescription ?? string.Empty,
            FullDescription = dto.FullDescription ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            TriggerRadius = dto.TriggerRadius,
            ApproachRadius = dto.ApproachRadius,
            Priority = dto.Priority,
            AudioUrl = dto.AudioUrl,
            TTSScript = dto.TTSScript,
            ImageUrl = dto.ImageUrl,
            MapLink = dto.MapLink,
            Language = dto.Language ?? "vi-VN",
            TourId = dto.TourId,
            OrderInTour = dto.OrderInTour,
            CooldownSeconds = dto.CooldownSeconds,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Tour MapToTour(TourDto dto)
    {
        return new Tour
        {
            Id = int.TryParse(dto.Id, out var id) ? id : 0,
            UniqueCode = dto.UniqueCode,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            EstimatedDistanceMeters = dto.DistanceKm * 1000, // Convert km to meters
            POICount = dto.POICount,
            ThumbnailUrl = dto.ThumbnailUrl,
            Language = dto.Language,
            DifficultyLevel = dto.DifficultyLevel,
            WheelchairAccessible = dto.WheelchairAccessible,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
