using HeriStepAI.API.Data;
using HeriStepAI.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Services;

public interface ISyncService
{
    Task<SyncResponseDto> SyncDataAsync(SyncRequestDto request);
    Task<ContentVersionDto> GetLatestVersionAsync();
}

public class SyncService : ISyncService
{
    private readonly AppDbContext _context;
    private readonly IPOIService _poiService;
    private readonly ITourService _tourService;

    public SyncService(AppDbContext context, IPOIService poiService, ITourService tourService)
    {
        _context = context;
        _poiService = poiService;
        _tourService = tourService;
    }

    public async Task<SyncResponseDto> SyncDataAsync(SyncRequestDto request)
    {
        var response = new SyncResponseDto
        {
            Success = true,
            SyncedAt = DateTime.UtcNow
        };

        // Get latest content version
        var latestVersion = await GetLatestVersionAsync();

        // Check if sync is needed
        if (request.LastSyncTime.HasValue)
        {
            var lastPOIUpdate = await _context.POIs
                .Where(p => p.UpdatedAt > request.LastSyncTime.Value)
                .AnyAsync();

            var lastTourUpdate = await _context.Tours
                .Where(t => t.UpdatedAt > request.LastSyncTime.Value)
                .AnyAsync();

            if (!lastPOIUpdate && !lastTourUpdate && 
                request.LastContentVersion == latestVersion.Version)
            {
                response.HasUpdates = false;
                response.ContentVersion = latestVersion;
                return response;
            }
        }

        response.HasUpdates = true;
        response.ContentVersion = latestVersion;

        // Get updated POIs
        if (request.IncludePOIs)
        {
            var poiQuery = _context.POIs
                .Include(p => p.Translations)
                .Where(p => p.IsActive);

            if (request.LastSyncTime.HasValue)
            {
                poiQuery = poiQuery.Where(p => p.UpdatedAt > request.LastSyncTime.Value);
            }

            if (request.BoundingBox != null)
            {
                poiQuery = poiQuery.Where(p =>
                    p.Latitude >= request.BoundingBox.SouthWest.Latitude &&
                    p.Latitude <= request.BoundingBox.NorthEast.Latitude &&
                    p.Longitude >= request.BoundingBox.SouthWest.Longitude &&
                    p.Longitude <= request.BoundingBox.NorthEast.Longitude);
            }

            var pois = await poiQuery.ToListAsync();
            response.POIs = pois.Select(MapToDto).ToList();
        }

        // Get updated Tours
        if (request.IncludeTours)
        {
            var tourQuery = _context.Tours
                .Include(t => t.POIIds)
                .Where(t => t.IsActive);

            if (request.LastSyncTime.HasValue)
            {
                tourQuery = tourQuery.Where(t => t.UpdatedAt > request.LastSyncTime.Value);
            }

            var tours = await tourQuery.ToListAsync();
            response.Tours = tours.Select(MapToDto).ToList();
        }

        // Get deleted IDs since last sync
        if (request.LastSyncTime.HasValue)
        {
            response.DeletedPOIIds = await _context.DeletedRecords
                .Where(d => d.EntityType == "POI" && d.DeletedAt > request.LastSyncTime.Value)
                .Select(d => d.EntityId)
                .ToListAsync();

            response.DeletedTourIds = await _context.DeletedRecords
                .Where(d => d.EntityType == "Tour" && d.DeletedAt > request.LastSyncTime.Value)
                .Select(d => d.EntityId)
                .ToListAsync();
        }

        return response;
    }

    public async Task<ContentVersionDto> GetLatestVersionAsync()
    {
        var lastPOIUpdate = await _context.POIs
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => p.UpdatedAt)
            .FirstOrDefaultAsync();

        var lastTourUpdate = await _context.Tours
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => t.UpdatedAt)
            .FirstOrDefaultAsync();

        var lastUpdate = lastPOIUpdate > lastTourUpdate ? lastPOIUpdate : lastTourUpdate;

        return new ContentVersionDto
        {
            Version = $"{lastUpdate:yyyyMMddHHmmss}",
            LastUpdated = lastUpdate,
            POICount = await _context.POIs.CountAsync(p => p.IsActive),
            TourCount = await _context.Tours.CountAsync(t => t.IsActive)
        };
    }

    private static POIDto MapToDto(POIEntity entity)
    {
        return new POIDto
        {
            Id = entity.Id,
            Name = entity.Name,
            ShortDescription = entity.ShortDescription,
            FullDescription = entity.FullDescription,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            TriggerRadiusMeters = entity.TriggerRadiusMeters,
            Priority = entity.Priority,
            Category = entity.Category,
            ImageUrl = entity.ImageUrl,
            ThumbnailUrl = entity.ThumbnailUrl,
            AudioUrl = entity.AudioUrl,
            AudioDurationSeconds = entity.AudioDurationSeconds,
            MapDeepLink = entity.MapDeepLink,
            IsActive = entity.IsActive,
            Translations = entity.Translations?.Select(t => new POITranslationDto
            {
                Language = t.Language,
                Name = t.Name,
                ShortDescription = t.ShortDescription,
                FullDescription = t.FullDescription,
                AudioUrl = t.AudioUrl
            }).ToList() ?? new List<POITranslationDto>()
        };
    }

    private static TourDto MapToDto(TourEntity entity)
    {
        return new TourDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            DistanceKm = entity.DistanceKm,
            ImageUrl = entity.ImageUrl,
            Difficulty = entity.Difficulty,
            IsActive = entity.IsActive,
            POIIds = entity.POIIds.OrderBy(p => p.Order).Select(p => p.POIId).ToList()
        };
    }
}

// Additional DTOs for sync operations
public class SyncRequestDto
{
    public DateTime? LastSyncTime { get; set; }
    public string? LastContentVersion { get; set; }
    public bool IncludePOIs { get; set; } = true;
    public bool IncludeTours { get; set; } = true;
    public BoundingBoxDto? BoundingBox { get; set; }
}

public class BoundingBoxDto
{
    public CoordinateDto NorthEast { get; set; } = null!;
    public CoordinateDto SouthWest { get; set; } = null!;
}

public class CoordinateDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class SyncResponseDto
{
    public bool Success { get; set; }
    public bool HasUpdates { get; set; }
    public DateTime SyncedAt { get; set; }
    public ContentVersionDto? ContentVersion { get; set; }
    public List<POIDto> POIs { get; set; } = new();
    public List<TourDto> Tours { get; set; } = new();
    public List<string> DeletedPOIIds { get; set; } = new();
    public List<string> DeletedTourIds { get; set; } = new();
}

public class ContentVersionDto
{
    public string Version { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int POICount { get; set; }
    public int TourCount { get; set; }
}

// Analytics DTOs
public class AnalyticsUploadDto
{
    public string AnonymousDeviceId { get; set; } = string.Empty;
    public List<LocationHistoryUploadDto> Locations { get; set; } = new();
    public List<NarrationHistoryUploadDto> Narrations { get; set; } = new();
}

public class LocationHistoryUploadDto
{
    public string SessionId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public double? Altitude { get; set; }
    public DateTime Timestamp { get; set; }
}

public class NarrationHistoryUploadDto
{
    public string SessionId { get; set; } = string.Empty;
    public string POIId { get; set; } = string.Empty;
    public string POIName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool Completed { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public double? TriggerDistance { get; set; }
    public double? TriggerLatitude { get; set; }
    public double? TriggerLongitude { get; set; }
}

public class DashboardAnalyticsDto
{
    public int TotalPOIs { get; set; }
    public int TotalTours { get; set; }
    public int TotalListens { get; set; }
    public int UniqueUsers { get; set; }
    public double AverageListenDurationSeconds { get; set; }
    public double CompletionRate { get; set; }
    public List<TopPOIDto> TopPOIs { get; set; } = new();
    public List<HeatmapPointDto> HeatmapData { get; set; } = new();
    public List<DailyStatsDto> DailyStats { get; set; } = new();
}

public class TopPOIDto
{
    public string POIId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ListenCount { get; set; }
    public double AvgDurationSeconds { get; set; }
    public double CompletionRate { get; set; }
}

public class HeatmapPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Weight { get; set; }
}

public class DailyStatsDto
{
    public DateTime Date { get; set; }
    public int ListenCount { get; set; }
    public int UniqueUsers { get; set; }
    public double AvgDurationSeconds { get; set; }
}
