using HeriStepAI.API.Data;
using HeriStepAI.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Services;

public interface IPOIService
{
    Task<List<POIDto>> GetAllAsync();
    Task<POIDto?> GetByIdAsync(string id);
    Task<List<POIDto>> GetByTourIdAsync(string tourId);
    Task<List<POIDto>> SearchAsync(string keyword);
    Task<List<POIDto>> GetNearbyAsync(double latitude, double longitude, double radiusMeters);
    Task<POIDto> CreateAsync(CreatePOIDto dto);
    Task<POIDto?> UpdateAsync(string id, UpdatePOIDto dto);
    Task<bool> DeleteAsync(string id);
}

public class POIService : IPOIService
{
    private readonly AppDbContext _context;

    public POIService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<POIDto>> GetAllAsync()
    {
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<POIDto?> GetByIdAsync(string id)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.POIs
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == intId);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<List<POIDto>> GetByTourIdAsync(string tourId)
    {
        if (!int.TryParse(tourId, out var intTourId))
            return new List<POIDto>();
            
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.TourId == intTourId && p.IsActive)
            .OrderBy(p => p.OrderInTour)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<POIDto>> SearchAsync(string keyword)
    {
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive && 
                (p.Name.Contains(keyword) || (p.ShortDescription != null && p.ShortDescription.Contains(keyword))))
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<POIDto>> GetNearbyAsync(double latitude, double longitude, double radiusMeters)
    {
        // Simple bounding box filter first
        var latDelta = radiusMeters / 111000.0; // Approximate degrees per meter for latitude
        var lonDelta = radiusMeters / (111000.0 * Math.Cos(latitude * Math.PI / 180));

        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive &&
                p.Latitude >= latitude - latDelta &&
                p.Latitude <= latitude + latDelta &&
                p.Longitude >= longitude - lonDelta &&
                p.Longitude <= longitude + lonDelta)
            .ToListAsync();

        // Filter by actual distance using Haversine formula
        var result = entities
            .Where(p => CalculateDistance(latitude, longitude, p.Latitude, p.Longitude) <= radiusMeters)
            .Select(MapToDto)
            .ToList();

        return result;
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public async Task<POIDto> CreateAsync(CreatePOIDto dto)
    {
        var entity = new POIEntity
        {
            UniqueCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
            Name = dto.Name,
            ShortDescription = dto.ShortDescription,
            FullDescription = dto.FullDescription,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            TriggerRadiusMeters = dto.TriggerRadiusMeters,
            TriggerRadius = dto.TriggerRadiusMeters,
            Priority = dto.Priority,
            AudioUrl = dto.AudioUrl,
            ImageUrl = dto.ImageUrl,
            Category = dto.Category,
            MapDeepLink = dto.MapDeepLink,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        _context.POIs.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<POIDto?> UpdateAsync(string id, UpdatePOIDto dto)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.POIs.FindAsync(intId);
        if (entity == null)
            return null;

        if (dto.Name != null) entity.Name = dto.Name;
        if (dto.ShortDescription != null) entity.ShortDescription = dto.ShortDescription;
        if (dto.FullDescription != null) entity.FullDescription = dto.FullDescription;
        if (dto.Latitude.HasValue) entity.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) entity.Longitude = dto.Longitude.Value;
        if (dto.TriggerRadiusMeters.HasValue) 
        {
            entity.TriggerRadiusMeters = dto.TriggerRadiusMeters.Value;
            entity.TriggerRadius = dto.TriggerRadiusMeters.Value;
        }
        if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
        if (dto.AudioUrl != null) entity.AudioUrl = dto.AudioUrl;
        if (dto.ImageUrl != null) entity.ImageUrl = dto.ImageUrl;
        if (dto.Category != null) entity.Category = dto.Category;
        if (dto.MapDeepLink != null) entity.MapDeepLink = dto.MapDeepLink;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out var intId))
            return false;
            
        var entity = await _context.POIs.FindAsync(intId);
        if (entity == null)
            return false;

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private static POIDto MapToDto(POIEntity entity)
    {
        return new POIDto
        {
            Id = entity.Id.ToString(),
            UniqueCode = entity.UniqueCode,
            Name = entity.Name,
            ShortDescription = entity.ShortDescription,
            FullDescription = entity.FullDescription,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            TriggerRadius = entity.TriggerRadius,
            TriggerRadiusMeters = entity.TriggerRadiusMeters,
            ApproachRadius = entity.ApproachRadius,
            Priority = entity.Priority,
            AudioUrl = entity.AudioUrl,
            AudioDurationSeconds = entity.AudioDurationSeconds,
            TTSScript = entity.TTSScript,
            ImageUrl = entity.ImageUrl,
            ThumbnailUrl = entity.ThumbnailUrl,
            MapLink = entity.MapLink,
            MapDeepLink = entity.MapDeepLink,
            Category = entity.Category,
            Language = entity.Language,
            TourId = entity.TourId,
            OrderInTour = entity.OrderInTour,
            CooldownSeconds = entity.CooldownSeconds,
            IsActive = entity.IsActive,
            Translations = entity.Translations?.Select(t => new POITranslationDto
            {
                Id = t.Id,
                POIId = t.POIId,
                LanguageCode = t.LanguageCode,
                Name = t.Name,
                ShortDescription = t.ShortDescription,
                FullDescription = t.FullDescription,
                TTSScript = t.TTSScript,
                AudioUrl = t.AudioUrl
            }).ToList()
        };
    }
}
