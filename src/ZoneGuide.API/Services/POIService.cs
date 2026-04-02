using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

public interface IPOIService
{
    Task<List<POIDto>> GetAllAsync(bool includeInactive = false);
    Task<POIDto?> GetByIdAsync(string id);
    Task<List<POITranslationDto>> GetTranslationsAsync(string poiId);
    Task<POITranslationDto?> UpsertTranslationAsync(string poiId, string languageCode, POITranslationDto dto);
    Task<bool> DeleteTranslationAsync(string poiId, string languageCode);
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

        public async Task<List<POIDto>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.POIs
                .Include(p => p.Translations)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(p => p.IsActive);
            }

            var entities = await query.OrderBy(p => p.Name).ToListAsync();

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

    public async Task<List<POITranslationDto>> GetTranslationsAsync(string poiId)
    {
        if (!int.TryParse(poiId, out var intPoiId))
            return new List<POITranslationDto>();

        return await _context.POITranslations
            .Where(t => t.POIId == intPoiId)
            .OrderBy(t => t.LanguageCode)
            .Select(t => new POITranslationDto
            {
                Id = t.Id,
                POIId = t.POIId,
                LanguageCode = t.LanguageCode,
                Name = t.Name,
                ShortDescription = t.ShortDescription,
                FullDescription = t.FullDescription,
                TTSScript = t.TTSScript,
                AudioUrl = t.AudioUrl
            })
            .ToListAsync();
    }

    public async Task<POITranslationDto?> UpsertTranslationAsync(string poiId, string languageCode, POITranslationDto dto)
    {
        if (!int.TryParse(poiId, out var intPoiId))
            return null;

        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode)
            ? dto.LanguageCode?.Trim()
            : languageCode.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
            return null;

        var poi = await _context.POIs.FirstOrDefaultAsync(p => p.Id == intPoiId);
        if (poi == null)
            return null;

        var existing = await _context.POITranslations
            .FirstOrDefaultAsync(t => t.POIId == intPoiId && t.LanguageCode == normalizedLanguageCode);

        if (existing == null)
        {
            existing = new POITranslationEntity
            {
                POIId = intPoiId,
                LanguageCode = normalizedLanguageCode,
                CreatedAt = DateTime.UtcNow
            };
            _context.POITranslations.Add(existing);
        }

        existing.Name = string.IsNullOrWhiteSpace(dto.Name) ? poi.Name : dto.Name;
        existing.ShortDescription = dto.ShortDescription ?? string.Empty;
        existing.FullDescription = dto.FullDescription ?? string.Empty;
        existing.TTSScript = dto.TTSScript;
        existing.AudioUrl = dto.AudioUrl;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new POITranslationDto
        {
            Id = existing.Id,
            POIId = existing.POIId,
            LanguageCode = existing.LanguageCode,
            Name = existing.Name,
            ShortDescription = existing.ShortDescription,
            FullDescription = existing.FullDescription,
            TTSScript = existing.TTSScript,
            AudioUrl = existing.AudioUrl
        };
    }

    public async Task<bool> DeleteTranslationAsync(string poiId, string languageCode)
    {
        if (!int.TryParse(poiId, out var intPoiId))
            return false;

        var normalizedLanguageCode = languageCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
            return false;

        var translation = await _context.POITranslations
            .FirstOrDefaultAsync(t => t.POIId == intPoiId && t.LanguageCode == normalizedLanguageCode);

        if (translation == null)
            return false;

        _context.POITranslations.Remove(translation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<POIDto>> GetByTourIdAsync(string tourId)
    {
        if (!int.TryParse(tourId, out var intTourId))
            return new List<POIDto>();

        var poiIdsByOrder = await _context.TourPOIs
            .Where(tp => tp.TourId == intTourId)
            .OrderBy(tp => tp.Order)
            .Select(tp => tp.POIId)
            .ToListAsync();

        if (!poiIdsByOrder.Any())
            return new List<POIDto>();

        var poiIdSet = poiIdsByOrder.ToHashSet();
        var poiById = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive && poiIdSet.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var entities = poiIdsByOrder
            .Where(id => poiById.ContainsKey(id))
            .Select(id => poiById[id])
            .ToList();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<POIDto>> SearchAsync(string keyword)
    {
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive && 
                (p.Name.Contains(keyword) ||
                 (p.Address != null && p.Address.Contains(keyword)) ||
                 (p.ShortDescription != null && p.ShortDescription.Contains(keyword))))
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
            Address = dto.Address,
            Name = dto.Name,
            ShortDescription = dto.ShortDescription,
            FullDescription = dto.FullDescription,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            TriggerRadius = dto.TriggerRadiusMeters,
            ApproachRadius = dto.ApproachRadius,
            Priority = dto.Priority,
            AudioUrl = dto.AudioUrl,
            TTSScript = dto.TTSScript,
            ImageUrl = dto.ImageUrl,
            Category = dto.Category,
            Language = dto.Language ?? "vi-VN",
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

        if (dto.Address != null) entity.Address = dto.Address;
        if (dto.Name != null) entity.Name = dto.Name;
        if (dto.ShortDescription != null) entity.ShortDescription = dto.ShortDescription;
        if (dto.FullDescription != null) entity.FullDescription = dto.FullDescription;
        if (dto.Latitude.HasValue) entity.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) entity.Longitude = dto.Longitude.Value;
        if (dto.TriggerRadiusMeters.HasValue) entity.TriggerRadius = dto.TriggerRadiusMeters.Value;
        if (dto.ApproachRadius.HasValue) entity.ApproachRadius = dto.ApproachRadius.Value;
        if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
        if (dto.AudioUrl != null) entity.AudioUrl = dto.AudioUrl;
        if (dto.TTSScript != null) entity.TTSScript = dto.TTSScript;
        if (dto.ImageUrl != null) entity.ImageUrl = dto.ImageUrl;
        if (dto.Category != null) entity.Category = dto.Category;
        if (dto.Language != null) entity.Language = dto.Language;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        // Cập nhật Translations nếu có
        if (dto.Translations != null)
        {
            // Load existing translations
            await _context.Entry(entity).Collection(e => e.Translations).LoadAsync();
            
            foreach (var transDto in dto.Translations)
            {
                var existing = entity.Translations.FirstOrDefault(t => t.LanguageCode == transDto.LanguageCode);
                if (existing != null)
                {
                    existing.Name = transDto.Name;
                    existing.ShortDescription = transDto.ShortDescription;
                    existing.FullDescription = transDto.FullDescription;
                    existing.TTSScript = transDto.TTSScript;
                    existing.AudioUrl = transDto.AudioUrl;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    entity.Translations.Add(new POITranslationEntity
                    {
                        POIId = entity.Id,
                        LanguageCode = transDto.LanguageCode,
                        Name = transDto.Name,
                        ShortDescription = transDto.ShortDescription,
                        FullDescription = transDto.FullDescription,
                        TTSScript = transDto.TTSScript,
                        AudioUrl = transDto.AudioUrl,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

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

        var deletedAt = DateTime.UtcNow;

        entity.IsActive = false;
        entity.UpdatedAt = deletedAt;

        var deletedRecord = await _context.DeletedRecords
            .FirstOrDefaultAsync(d => d.EntityType == "POI" && d.EntityId == entity.Id.ToString());

        if (deletedRecord == null)
        {
            _context.DeletedRecords.Add(new DeletedRecordEntity
            {
                EntityType = "POI",
                EntityId = entity.Id.ToString(),
                DeletedAt = deletedAt
            });
        }
        else
        {
            deletedRecord.DeletedAt = deletedAt;
        }

        await _context.SaveChangesAsync();
        return true;
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
            AudioUrl = entity.AudioUrl,
            TTSScript = entity.TTSScript,
            ImageUrl = entity.ImageUrl,
            MapLink = entity.MapLink,
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
