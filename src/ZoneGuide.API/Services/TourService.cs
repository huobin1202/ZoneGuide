using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

public interface ITourService
{
    Task<List<TourDto>> GetAllAsync(bool includeInactive = false);
    Task<TourDto?> GetByIdAsync(string id);
    Task<TourDto?> GetWithPOIsAsync(string id);
    Task<List<TourTranslationDto>> GetTranslationsAsync(string tourId);
    Task<TourTranslationDto?> UpsertTranslationAsync(string tourId, string languageCode, TourTranslationDto dto);
    Task<bool> DeleteTranslationAsync(string tourId, string languageCode);
    Task<TourDto> CreateAsync(CreateTourDto dto);
    Task<TourDto?> UpdateAsync(string id, UpdateTourDto dto);
    Task<bool> DeleteAsync(string id);
    Task<TourDto?> ReorderPOIsAsync(string id, List<string> poiIds);
}

public class TourService : ITourService
{
    private readonly AppDbContext _context;
    private readonly IPOIService _poiService;

    public TourService(AppDbContext context, IPOIService poiService)
    {
        _context = context;
        _poiService = poiService;
    }

    public async Task<List<TourDto>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.POIIds)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var entities = await query
            .OrderBy(t => t.Name)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<TourDto?> GetByIdAsync(string id)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.POIIds)
            .ThenInclude(tp => tp.POI)
            .FirstOrDefaultAsync(t => t.Id == intId);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<TourDto?> GetWithPOIsAsync(string id)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.POIIds)
            .ThenInclude(tp => tp.POI)
            .FirstOrDefaultAsync(t => t.Id == intId);
        if (entity == null)
            return null;

        var dto = MapToDto(entity);
        dto.POIs = await _poiService.GetByTourIdAsync(id);
        return dto;
    }

    public async Task<List<TourTranslationDto>> GetTranslationsAsync(string tourId)
    {
        if (!int.TryParse(tourId, out var intTourId))
            return new List<TourTranslationDto>();

        return await _context.TourTranslations
            .Where(t => t.TourId == intTourId)
            .OrderBy(t => t.LanguageCode)
            .Select(t => new TourTranslationDto
            {
                Id = t.Id,
                TourId = t.TourId,
                LanguageCode = t.LanguageCode,
                Description = t.Description,
                IsOutdated = t.IsOutdated
            })
            .ToListAsync();
    }

    public async Task<TourTranslationDto?> UpsertTranslationAsync(string tourId, string languageCode, TourTranslationDto dto)
    {
        if (!int.TryParse(tourId, out var intTourId))
            return null;

        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode)
            ? dto.LanguageCode?.Trim()
            : languageCode.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
            return null;

        var tour = await _context.Tours.FirstOrDefaultAsync(t => t.Id == intTourId);
        if (tour == null)
            return null;

        var existing = await _context.TourTranslations
            .FirstOrDefaultAsync(t => t.TourId == intTourId && t.LanguageCode == normalizedLanguageCode);

        if (existing == null)
        {
            existing = new TourTranslationEntity
            {
                TourId = intTourId,
                LanguageCode = normalizedLanguageCode,
                CreatedAt = DateTime.UtcNow,
                IsOutdated = false
            };
            _context.TourTranslations.Add(existing);
        }

        existing.Description = dto.Description ?? string.Empty;
        existing.IsOutdated = false;
        existing.UpdatedAt = DateTime.UtcNow;
        tour.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new TourTranslationDto
        {
            Id = existing.Id,
            TourId = existing.TourId,
            LanguageCode = existing.LanguageCode,
            Description = existing.Description,
            IsOutdated = existing.IsOutdated
        };
    }

    public async Task<bool> DeleteTranslationAsync(string tourId, string languageCode)
    {
        if (!int.TryParse(tourId, out var intTourId))
            return false;

        var normalizedLanguageCode = languageCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
            return false;

        var translation = await _context.TourTranslations
            .FirstOrDefaultAsync(t => t.TourId == intTourId && t.LanguageCode == normalizedLanguageCode);

        if (translation == null)
            return false;

        _context.TourTranslations.Remove(translation);

        var tour = await _context.Tours.FirstOrDefaultAsync(t => t.Id == intTourId);
        if (tour != null)
        {
            tour.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TourDto> CreateAsync(CreateTourDto dto)
    {
        var entity = new TourEntity
        {
            UniqueCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
            Name = dto.Name,
            Description = dto.Description,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            DistanceKm = dto.DistanceKm,
            ImageUrl = dto.ImageUrl,
            POICount = dto.POIIds?.Count ?? 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Tours.Add(entity);
        await _context.SaveChangesAsync();

        // Add POI associations
        if (dto.POIIds != null && dto.POIIds.Any())
        {
            for (int i = 0; i < dto.POIIds.Count; i++)
            {
                if (int.TryParse(dto.POIIds[i], out var poiIntId))
                {
                    _context.TourPOIs.Add(new TourPOIEntity
                    {
                        TourId = entity.Id,
                        POIId = poiIntId,
                        Order = i
                    });

                    var poi = await _context.POIs.FindAsync(poiIntId);
                    if (poi != null)
                    {
                        poi.TourId = entity.Id;
                        poi.OrderInTour = i;
                        poi.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        // Reload with POIIds
        await _context.Entry(entity).Collection(e => e.POIIds).LoadAsync();

        return MapToDto(entity);
    }

    public async Task<TourDto?> UpdateAsync(string id, UpdateTourDto dto)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.POIIds)
            .ThenInclude(tp => tp.POI)
            .FirstOrDefaultAsync(t => t.Id == intId);
        if (entity == null)
            return null;

        if (dto.Name != null) entity.Name = dto.Name;
        if (dto.Description != null) entity.Description = dto.Description;
        if (dto.EstimatedDurationMinutes.HasValue) entity.EstimatedDurationMinutes = dto.EstimatedDurationMinutes.Value;
        if (dto.DistanceKm.HasValue) entity.DistanceKm = dto.DistanceKm.Value;
        if (dto.ImageUrl != null) entity.ImageUrl = dto.ImageUrl;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

        if (dto.Description != null)
        {
            foreach (var translation in entity.Translations)
            {
                translation.IsOutdated = true;
                translation.UpdatedAt = DateTime.UtcNow;
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;

        // Update POI associations if provided
        if (dto.POIIds != null)
        {
            var oldPoiIds = entity.POIIds.Select(p => p.POIId).ToList();

            // Remove existing POI associations
            _context.TourPOIs.RemoveRange(entity.POIIds);

            foreach (var oldPoiId in oldPoiIds)
            {
                var oldPoi = await _context.POIs.FindAsync(oldPoiId);
                if (oldPoi != null && oldPoi.TourId == intId)
                {
                    oldPoi.TourId = null;
                    oldPoi.OrderInTour = 0;
                    oldPoi.UpdatedAt = DateTime.UtcNow;
                }
            }
            
            // Add new associations with order
            for (int i = 0; i < dto.POIIds.Count; i++)
            {
                if (int.TryParse(dto.POIIds[i], out var poiIntId))
                {
                    entity.POIIds.Add(new TourPOIEntity
                    {
                        TourId = intId,
                        POIId = poiIntId,
                        Order = i
                    });

                    var poi = await _context.POIs.FindAsync(poiIntId);
                    if (poi != null)
                    {
                        poi.TourId = intId;
                        poi.OrderInTour = i;
                        poi.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            entity.POICount = dto.POIIds.Count;
        }

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out var intId))
            return false;
            
        var entity = await _context.Tours.FindAsync(intId);
        if (entity == null)
            return false;

        var deletedAt = DateTime.UtcNow;
        entity.IsActive = false;
        entity.UpdatedAt = deletedAt;

        var deletedRecord = await _context.DeletedRecords
            .FirstOrDefaultAsync(d => d.EntityType == "Tour" && d.EntityId == entity.Id.ToString());

        if (deletedRecord == null)
        {
            _context.DeletedRecords.Add(new DeletedRecordEntity
            {
                EntityType = "Tour",
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

    public async Task<TourDto?> ReorderPOIsAsync(string id, List<string> poiIds)
    {
        if (!int.TryParse(id, out var intId))
            return null;
            
        var entity = await _context.Tours
            .Include(t => t.Translations)
            .Include(t => t.POIIds)
            .ThenInclude(tp => tp.POI)
            .FirstOrDefaultAsync(t => t.Id == intId);
            
        if (entity == null)
            return null;

        // Remove existing POI associations
        var oldPoiIds = entity.POIIds.Select(p => p.POIId).ToList();
        _context.TourPOIs.RemoveRange(entity.POIIds);

        foreach (var oldPoiId in oldPoiIds)
        {
            var oldPoi = await _context.POIs.FindAsync(oldPoiId);
            if (oldPoi != null && oldPoi.TourId == intId)
            {
                oldPoi.TourId = null;
                oldPoi.OrderInTour = 0;
                oldPoi.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Add new associations with order
        for (int i = 0; i < poiIds.Count; i++)
        {
            if (int.TryParse(poiIds[i], out var poiIntId))
            {
                entity.POIIds.Add(new TourPOIEntity
                {
                    TourId = intId,
                    POIId = poiIntId,
                    Order = i
                });

                var poi = await _context.POIs.FindAsync(poiIntId);
                if (poi != null)
                {
                    poi.TourId = intId;
                    poi.OrderInTour = i;
                    poi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        entity.POICount = poiIds.Count;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    private static TourDto MapToDto(TourEntity entity)
    {
        var activePoiIds = entity.POIIds
            .Where(tp => tp.POI == null || tp.POI.IsActive)
            .OrderBy(tp => tp.Order)
            .Select(tp => tp.POIId.ToString())
            .ToList();

        return new TourDto
        {
            Id = entity.Id.ToString(),
            UniqueCode = entity.UniqueCode,
            Name = entity.Name,
            Description = entity.Description,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            DistanceKm = entity.DistanceKm,
            ImageUrl = entity.ImageUrl,
            ThumbnailUrl = entity.ThumbnailUrl,
            Language = entity.Language,
            WheelchairAccessible = entity.WheelchairAccessible,
            IsActive = entity.IsActive,
            Translations = entity.Translations
                .OrderBy(t => t.LanguageCode)
                .Select(t => new TourTranslationDto
                {
                    Id = t.Id,
                    TourId = t.TourId,
                    LanguageCode = t.LanguageCode,
                    Description = t.Description,
                    IsOutdated = t.IsOutdated
                })
                .ToList(),
            POICount = activePoiIds.Count,
            POIIds = activePoiIds
        };
    }
}
