using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service đồng bộ dữ liệu
/// </summary>
public class SyncService : ISyncService
{
    private const string LastSyncLanguageKey = "last_sync_language";
    public event EventHandler? SyncStarted;
    public event EventHandler<bool>? SyncCompleted;
    public event EventHandler<double>? SyncProgress;

    private readonly ApiService _apiService;
    private readonly IPOIRepository _poiRepository;
    private readonly IPOITranslationRepository _poiTranslationRepository;
    private readonly ITourRepository _tourRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ISettingsService _settingsService;
    private readonly INarrationService _narrationService;
    private readonly SemaphoreSlim _syncStateLock = new(1, 1);
    private bool _hasLoadedLastSyncTime;

    public bool IsSyncing { get; private set; }
    public DateTime? LastSyncTime { get; private set; }

    public SyncService(
        ApiService apiService,
        IPOIRepository poiRepository,
        IPOITranslationRepository poiTranslationRepository,
        ITourRepository tourRepository,
        IAnalyticsRepository analyticsRepository,
        ISettingsService settingsService,
        INarrationService narrationService)
    {
        _apiService = apiService;
        _poiRepository = poiRepository;
        _poiTranslationRepository = poiTranslationRepository;
        _tourRepository = tourRepository;
        _analyticsRepository = analyticsRepository;
        _settingsService = settingsService;
        _narrationService = narrationService;
    }

    public async Task<bool> SyncFromServerAsync()
    {
        if (IsSyncing)
            return false;

        try
        {
            await EnsureSyncStateLoadedAsync();

            var currentLanguage = NormalizeLanguage(_settingsService.Settings.PreferredLanguage);
            var previousLanguage = NormalizeLanguage(await _settingsService.GetAsync<string>(LastSyncLanguageKey));
            var forceFullSync = !string.Equals(currentLanguage, previousLanguage, StringComparison.OrdinalIgnoreCase);

            IsSyncing = true;
            SyncStarted?.Invoke(this, EventArgs.Empty);

            var request = new SyncRequest
            {
                LastSyncTime = forceFullSync ? null : LastSyncTime,
                Language = currentLanguage
            };

            SyncProgress?.Invoke(this, 0.1);

            var syncData = await _apiService.SyncDataAsync(request);
            
            if (syncData == null)
            {
                SyncCompleted?.Invoke(this, false);
                return false;
            }

            var localActivePoisBeforeSync = await _poiRepository.GetActiveAsync();
            var incomingPoiIds = syncData.POIs
                .Select(p => int.TryParse(p.Id, out var parsed) ? parsed : 0)
                .Where(id => id > 0)
                .ToHashSet();

            var effectiveDeletedPoiIds = syncData.DeletedPOIIds
                .Where(id => !incomingPoiIds.Contains(id))
                .Distinct()
                .ToList();

            var looksLikeMassDeletion = localActivePoisBeforeSync.Count > 0
                && incomingPoiIds.Count == 0
                && effectiveDeletedPoiIds.Count >= Math.Max(3, (int)Math.Ceiling(localActivePoisBeforeSync.Count * 0.7));

            if (looksLikeMassDeletion)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SyncService] Blocked suspicious sync delete. localActive={localActivePoisBeforeSync.Count}, incoming={incomingPoiIds.Count}, deleted={effectiveDeletedPoiIds.Count}");

                SyncCompleted?.Invoke(this, false);
                return false;
            }

            SyncProgress?.Invoke(this, 0.3);

            // Xóa các POI đã bị xóa trên server
            foreach (var deletedId in effectiveDeletedPoiIds)
            {
                await _poiRepository.DeleteAsync(deletedId);
            }

            SyncProgress?.Invoke(this, 0.5);

            // Cập nhật POIs
            foreach (var poiDto in syncData.POIs)
            {
                var poi = MapToPOI(poiDto);
                await _poiRepository.InsertOrUpdateAsync(poi);
                await SyncPoiTranslationsAsync(poiDto, poi.Id);
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
            await _settingsService.SetAsync(LastSyncLanguageKey, currentLanguage);

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
            var tour = await _apiService.GetTourDetailsAsync(tourId);

            var poisToDownload = tour?.POIs?.ToList() ?? new List<POIDto>();
            if (poisToDownload.Count == 0)
            {
                var localPois = await _poiRepository.GetByTourIdAsync(tourId);
                poisToDownload = localPois
                    .Select(p => new POIDto
                    {
                        Id = p.Id.ToString(),
                        AudioUrl = p.AudioUrl,
                        ImageUrl = p.ImageUrl
                    })
                    .ToList();
            }

            if (poisToDownload.Count == 0)
                return false;

            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());
            Directory.CreateDirectory(offlineDir);

            var downloadableAssetCount = 0;
            var downloadedAssetCount = 0;

            foreach (var poi in poisToDownload)
            {
                string? savedAudioPath = null;
                string? savedImagePath = null;

                POI? localPoi = null;
                if (int.TryParse(poi.Id, out var poiIdForLookup))
                {
                    localPoi = await _poiRepository.GetByIdAsync(poiIdForLookup);
                }

                var resolvedAudioUrl = !string.IsNullOrWhiteSpace(poi.AudioUrl)
                    ? poi.AudioUrl
                    : localPoi?.AudioUrl;

                var resolvedImageUrl = !string.IsNullOrWhiteSpace(poi.ImageUrl)
                    ? poi.ImageUrl
                    : localPoi?.ImageUrl;

                // Tải audio
                if (!string.IsNullOrWhiteSpace(resolvedAudioUrl))
                {
                    downloadableAssetCount++;
                    var audioData = await _apiService.DownloadAudioAsync(resolvedAudioUrl);
                    if (audioData != null)
                    {
                        var audioPath = Path.Combine(offlineDir, $"audio_{poi.Id}.mp3");
                        await File.WriteAllBytesAsync(audioPath, audioData);
                        savedAudioPath = audioPath;
                        downloadedAssetCount++;
                    }
                }

                // Tải ảnh
                if (!string.IsNullOrWhiteSpace(resolvedImageUrl))
                {
                    downloadableAssetCount++;
                    var imageData = await _apiService.DownloadImageAsync(resolvedImageUrl);
                    if (imageData != null)
                    {
                        var imagePath = Path.Combine(offlineDir, $"image_{poi.Id}.jpg");
                        await File.WriteAllBytesAsync(imagePath, imageData);
                        savedImagePath = imagePath;
                        downloadedAssetCount++;
                    }
                }

                if (int.TryParse(poi.Id, out var poiId))
                {
                    localPoi ??= await _poiRepository.GetByIdAsync(poiId);
                    if (localPoi != null)
                    {
                        if (!string.IsNullOrWhiteSpace(savedAudioPath))
                        {
                            localPoi.AudioFilePath = savedAudioPath;
                        }

                        if (!string.IsNullOrWhiteSpace(savedImagePath))
                        {
                            localPoi.ImagePath = savedImagePath;
                        }

                        await _poiRepository.UpdateAsync(localPoi);
                    }
                }
            }

            var manifestPath = Path.Combine(offlineDir, "manifest.json");
            var manifestJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                TourId = tourId,
                POICount = poisToDownload.Count,
                DownloadableAssetCount = downloadableAssetCount,
                DownloadedAssetCount = downloadedAssetCount,
                CreatedAt = DateTime.UtcNow
            });
            await File.WriteAllTextAsync(manifestPath, manifestJson);

            if (downloadableAssetCount == 0)
            {
                // Tour có thể chỉ dùng TTS nên không có file media để tải.
                return true;
            }

            return downloadedAssetCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteTourOfflineAsync(int tourId)
    {
        try
        {
            var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());

            if (_narrationService.CurrentItem?.POI.TourId == tourId)
            {
                await _narrationService.StopAsync();
            }

            var tourPois = await _poiRepository.GetByTourIdAsync(tourId);
            foreach (var poi in tourPois)
            {
                poi.AudioFilePath = null;

                if (!string.IsNullOrWhiteSpace(poi.ImagePath) &&
                    poi.ImagePath.StartsWith(offlineDir, StringComparison.OrdinalIgnoreCase))
                {
                    poi.ImagePath = null;
                }

                await _poiRepository.UpdateAsync(poi);
            }

            if (Directory.Exists(offlineDir))
            {
                Directory.Delete(offlineDir, true);
            }

            return !Directory.Exists(offlineDir);
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> IsTourOfflineAvailableAsync(int tourId)
    {
        var offlineDir = Path.Combine(FileSystem.AppDataDirectory, "offline", tourId.ToString());
        if (!Directory.Exists(offlineDir))
            return Task.FromResult(false);

        var hasFiles = Directory
            .EnumerateFiles(offlineDir, "*.*", SearchOption.TopDirectoryOnly)
            .Any();

        return Task.FromResult(hasFiles);
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
            Category = ResolveCategory(dto),
            Language = dto.Language ?? "vi-VN",
            TourId = dto.TourId,
            OrderInTour = dto.OrderInTour,
            CooldownSeconds = dto.CooldownSeconds,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string ResolveCategory(POIDto dto)
    {
        // Nếu server đã gửi category (kể cả "Khác"/"other") thì tôn trọng dữ liệu đó.
        if (!string.IsNullOrWhiteSpace(dto.Category))
            return NormalizeCategory(dto.Category);

        var combined = $"{dto.UniqueCode} {dto.Name} {dto.ShortDescription}".ToLowerInvariant();

        if (combined.Contains("ẩm thực") || combined.Contains("food") || combined.Contains("ăn uống") || combined.Contains("restaurant") || combined.Contains("vĩnh khánh"))
            return "food";

        if (combined.Contains("dịch vụ") || combined.Contains("service") || combined.Contains("hospital") || combined.Contains("hotel") || combined.Contains("spa"))
            return "service";

        if (combined.Contains("giải trí") || combined.Contains("entertainment") || combined.Contains("cinema") || combined.Contains("công viên") || combined.Contains("park"))
            return "entertainment";

        if (combined.Contains("mua sắm") || combined.Contains("shopping") || combined.Contains("mall") || combined.Contains("chợ") || combined.Contains("market"))
            return "shopping";

        if (combined.Contains("di tích") || combined.Contains("bảo tàng") || combined.Contains("đình") || combined.Contains("chùa") || combined.Contains("nhà rồng") || combined.Contains("cầu mống"))
            return "tourism";

        return "other";
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "other";

        return category.Trim().ToLowerInvariant() switch
        {
            "tourism" or "du lịch" => "tourism",
            "service" or "services" or "dịch vụ" => "service",
            "food" or "food & drink" or "ăn uống" => "food",
            "entertainment" or "giải trí" => "entertainment",
            "shopping" or "mua sắm" => "shopping",
            _ => "other"
        };
    }

    private static Tour MapToTour(TourDto dto)
    {
        var thumbnailUrl = !string.IsNullOrWhiteSpace(dto.ThumbnailUrl)
            ? dto.ThumbnailUrl
            : dto.ImageUrl;

        return new Tour
        {
            Id = int.TryParse(dto.Id, out var id) ? id : 0,
            UniqueCode = dto.UniqueCode,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            EstimatedDistanceMeters = dto.DistanceKm * 1000, // Convert km to meters
            POICount = dto.POICount,
            ThumbnailUrl = thumbnailUrl,
            Language = dto.Language,
            DifficultyLevel = dto.DifficultyLevel,
            WheelchairAccessible = dto.WheelchairAccessible,
            IsActive = dto.IsActive,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task SyncPoiTranslationsAsync(POIDto poiDto, int poiId)
    {
        if (poiId <= 0)
            return;

        var incoming = poiDto.Translations ?? new List<POITranslationDto>();
        var existing = await _poiTranslationRepository.GetByPOIIdAsync(poiId);

        var incomingKeys = incoming
            .Select(t => NormalizeLanguage(t.LanguageCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var old in existing)
        {
            if (!incomingKeys.Contains(NormalizeLanguage(old.LanguageCode)))
            {
                await _poiTranslationRepository.DeleteAsync(old.Id);
            }
        }

        foreach (var translation in incoming)
        {
            if (string.IsNullOrWhiteSpace(translation.LanguageCode))
                continue;

            var entry = new POITranslation
            {
                POIId = poiId,
                LanguageCode = NormalizeLanguage(translation.LanguageCode),
                Name = translation.Name ?? string.Empty,
                ShortDescription = translation.ShortDescription ?? string.Empty,
                FullDescription = translation.FullDescription ?? string.Empty,
                TTSScript = translation.TTSScript,
                AudioUrl = translation.AudioUrl
            };

            await _poiTranslationRepository.InsertOrUpdateAsync(entry);
        }
    }

    private static string NormalizeLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "vi-VN";

        var value = code.Trim().Replace('_', '-');
        return value.ToLowerInvariant() switch
        {
            var c when c.StartsWith("vi") => "vi-VN",
            var c when c.StartsWith("en") => "en-US",
            var c when c.StartsWith("zh") => "zh-CN",
            var c when c.StartsWith("ja") => "ja-JP",
            var c when c.StartsWith("ko") => "ko-KR",
            var c when c.StartsWith("fr") => "fr-FR",
            _ => value
        };
    }

    private async Task EnsureSyncStateLoadedAsync()
    {
        if (_hasLoadedLastSyncTime)
            return;

        await _syncStateLock.WaitAsync();
        try
        {
            if (_hasLoadedLastSyncTime)
                return;

            LastSyncTime = await _settingsService.GetAsync<DateTime?>("last_sync_time");
            _hasLoadedLastSyncTime = true;
        }
        finally
        {
            _syncStateLock.Release();
        }
    }

}
