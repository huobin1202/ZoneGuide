using HeriStepAI.API.Data;
using HeriStepAI.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Services;

public interface ITourService
{
    Task<List<TourDto>> GetAllAsync();
    Task<TourDto?> GetByIdAsync(int id);
    Task<TourDto?> GetWithPOIsAsync(int id);
    Task<TourDto> CreateAsync(TourDto dto);
    Task<TourDto?> UpdateAsync(int id, TourDto dto);
    Task<bool> DeleteAsync(int id);
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

    public async Task<List<TourDto>> GetAllAsync()
    {
        var entities = await _context.Tours
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<TourDto?> GetByIdAsync(int id)
    {
        var entity = await _context.Tours.FindAsync(id);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<TourDto?> GetWithPOIsAsync(int id)
    {
        var entity = await _context.Tours.FindAsync(id);
        if (entity == null)
            return null;

        var dto = MapToDto(entity);
        dto.POIs = await _poiService.GetByTourIdAsync(id);
        return dto;
    }

    public async Task<TourDto> CreateAsync(TourDto dto)
    {
        var entity = MapToEntity(dto);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.Tours.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<TourDto?> UpdateAsync(int id, TourDto dto)
    {
        var entity = await _context.Tours.FindAsync(id);
        if (entity == null)
            return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        entity.EstimatedDistanceMeters = dto.EstimatedDistanceMeters;
        entity.POICount = dto.POICount;
        entity.ThumbnailUrl = dto.ThumbnailUrl;
        entity.Language = dto.Language;
        entity.DifficultyLevel = dto.DifficultyLevel;
        entity.WheelchairAccessible = dto.WheelchairAccessible;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Tours.FindAsync(id);
        if (entity == null)
            return false;

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private static TourDto MapToDto(TourEntity entity)
    {
        return new TourDto
        {
            Id = entity.Id,
            UniqueCode = entity.UniqueCode,
            Name = entity.Name,
            Description = entity.Description,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            EstimatedDistanceMeters = entity.EstimatedDistanceMeters,
            POICount = entity.POICount,
            ThumbnailUrl = entity.ThumbnailUrl,
            Language = entity.Language,
            DifficultyLevel = entity.DifficultyLevel,
            WheelchairAccessible = entity.WheelchairAccessible,
            IsActive = entity.IsActive
        };
    }

    private static TourEntity MapToEntity(TourDto dto)
    {
        return new TourEntity
        {
            UniqueCode = dto.UniqueCode,
            Name = dto.Name,
            Description = dto.Description,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            EstimatedDistanceMeters = dto.EstimatedDistanceMeters,
            POICount = dto.POICount,
            ThumbnailUrl = dto.ThumbnailUrl,
            Language = dto.Language,
            DifficultyLevel = dto.DifficultyLevel,
            WheelchairAccessible = dto.WheelchairAccessible,
            IsActive = dto.IsActive
        };
    }
}
