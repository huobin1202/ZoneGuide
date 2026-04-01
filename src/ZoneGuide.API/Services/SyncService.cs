using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

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

            var deletedTourRecords = await _context.DeletedRecords
                .Where(d => d.EntityType == "Tour" && d.DeletedAt > request.LastSyncTime.Value)
                .Select(d => d.EntityId)
                .ToListAsync();

            var inactiveTourIds = await _context.Tours
                .Where(t => !t.IsActive)
                .Select(t => t.Id.ToString())
                .ToListAsync();

            response.DeletedTourIds = deletedTourRecords
                .Concat(inactiveTourIds)
                .Distinct()
                .ToList();
        }
        else
        {
            response.DeletedTourIds = await _context.Tours
                .Where(t => !t.IsActive)
                .Select(t => t.Id.ToString())
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
            Id = entity.Id.ToString(),
            UniqueCode = entity.UniqueCode,
            Address = entity.Address,
            Name = entity.Name,
            ShortDescription = entity.ShortDescription,
            FullDescription = entity.FullDescription,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            TriggerRadiusMeters = entity.TriggerRadius,
            TriggerRadius = entity.TriggerRadius,
            ApproachRadius = entity.ApproachRadius,
            Priority = entity.Priority,
            Category = entity.Category,
            ImageUrl = entity.ImageUrl,
            AudioUrl = entity.AudioUrl,
            TTSScript = entity.TTSScript,
            MapLink = entity.MapLink,
            Language = entity.Language,
            TourId = entity.TourId,
            OrderInTour = entity.OrderInTour,
            CooldownSeconds = entity.CooldownSeconds,
            IsActive = entity.IsActive,
            Translations = entity.Translations?.Select(t => new POITranslationDto
            {
                LanguageCode = t.LanguageCode,
                Name = t.Name,
                ShortDescription = t.ShortDescription ?? string.Empty,
                FullDescription = t.FullDescription ?? string.Empty,
                AudioUrl = t.AudioUrl
            }).ToList() ?? new List<POITranslationDto>()
        };
    }

    private static TourDto MapToDto(TourEntity entity)
    {
        return new TourDto
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            Description = entity.Description,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            DistanceKm = entity.DistanceKm,
            ImageUrl = entity.ImageUrl,
            Difficulty = entity.Difficulty,
            IsActive = entity.IsActive,
            POIIds = entity.POIIds.OrderBy(p => p.Order).Select(p => p.POIId.ToString()).ToList()
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

// Note: DashboardAnalyticsDto, TopPOIDto, HeatmapPointDto, DailyStatsDto are defined in ZoneGuide.Shared.Models
