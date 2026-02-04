using HeriStepAI.API.Data;
using HeriStepAI.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Services;

public interface IPOIService
{
    Task<List<POIDto>> GetAllAsync();
    Task<POIDto?> GetByIdAsync(int id);
    Task<List<POIDto>> GetByTourIdAsync(int tourId);
    Task<List<POIDto>> SearchAsync(string keyword);
    Task<POIDto> CreateAsync(POIDto dto);
    Task<POIDto?> UpdateAsync(int id, POIDto dto);
    Task<bool> DeleteAsync(int id);
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

    public async Task<POIDto?> GetByIdAsync(int id)
    {
        var entity = await _context.POIs
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == id);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<List<POIDto>> GetByTourIdAsync(int tourId)
    {
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.TourId == tourId && p.IsActive)
            .OrderBy(p => p.OrderInTour)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<POIDto>> SearchAsync(string keyword)
    {
        var entities = await _context.POIs
            .Include(p => p.Translations)
            .Where(p => p.IsActive && 
                (p.Name.Contains(keyword) || p.ShortDescription.Contains(keyword)))
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<POIDto> CreateAsync(POIDto dto)
    {
        var entity = MapToEntity(dto);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _context.POIs.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<POIDto?> UpdateAsync(int id, POIDto dto)
    {
        var entity = await _context.POIs.FindAsync(id);
        if (entity == null)
            return null;

        entity.Name = dto.Name;
        entity.ShortDescription = dto.ShortDescription;
        entity.FullDescription = dto.FullDescription;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.TriggerRadius = dto.TriggerRadius;
        entity.ApproachRadius = dto.ApproachRadius;
        entity.Priority = dto.Priority;
        entity.AudioUrl = dto.AudioUrl;
        entity.TTSScript = dto.TTSScript;
        entity.ImageUrl = dto.ImageUrl;
        entity.MapLink = dto.MapLink;
        entity.Language = dto.Language;
        entity.TourId = dto.TourId;
        entity.OrderInTour = dto.OrderInTour;
        entity.CooldownSeconds = dto.CooldownSeconds;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.POIs.FindAsync(id);
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
            Id = entity.Id,
            UniqueCode = entity.UniqueCode,
            Name = entity.Name,
            ShortDescription = entity.ShortDescription,
            FullDescription = entity.FullDescription,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            TriggerRadius = entity.TriggerRadius,
            ApproachRadius = entity.ApproachRadius,
            Priority = entity.Priority,
            AudioUrl = entity.AudioUrl,
            TTSScript = entity.TTSScript,
            ImageUrl = entity.ImageUrl,
            MapLink = entity.MapLink,
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

    private static POIEntity MapToEntity(POIDto dto)
    {
        return new POIEntity
        {
            UniqueCode = dto.UniqueCode,
            Name = dto.Name,
            ShortDescription = dto.ShortDescription,
            FullDescription = dto.FullDescription,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            TriggerRadius = dto.TriggerRadius,
            ApproachRadius = dto.ApproachRadius,
            Priority = dto.Priority,
            AudioUrl = dto.AudioUrl,
            TTSScript = dto.TTSScript,
            ImageUrl = dto.ImageUrl,
            MapLink = dto.MapLink,
            Language = dto.Language,
            TourId = dto.TourId,
            OrderInTour = dto.OrderInTour,
            CooldownSeconds = dto.CooldownSeconds,
            IsActive = dto.IsActive
        };
    }
}
