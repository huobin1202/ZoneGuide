using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToursController : ControllerBase
{
    private readonly ITourService _tourService;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<ToursController> _logger;

    public ToursController(ITourService tourService, IActivityLogService activityLogService, ILogger<ToursController> logger)
    {
        _tourService = tourService;
        _activityLogService = activityLogService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active tours
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TourDto>>> GetAll()
    {
        try
        {
            var tours = await _tourService.GetAllAsync();
            return Ok(tours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tours");
            return StatusCode(500, "An error occurred while retrieving tours");
        }
    }

    /// <summary>
    /// Get all tours (Admin endpoint to get both active and inactive)
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<List<TourDto>>> GetAllAdmin()
    {
        try
        {
            var tours = await _tourService.GetAllAsync(includeInactive: true);
            return Ok(tours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin tours");
            return StatusCode(500, "An error occurred while retrieving tours");
        }
    }

    /// <summary>
    /// Get tour by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TourDto>> GetById(string id)
    {
        try
        {
            var tour = await _tourService.GetByIdAsync(id);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            return Ok(tour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tour {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the tour");
        }
    }

    /// <summary>
    /// Get tour with full POI details
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<TourWithPOIsDto>> GetWithDetails(string id)
    {
        try
        {
            var tour = await _tourService.GetWithPOIsAsync(id);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            return Ok(tour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tour details {Id}", id);
            return StatusCode(500, "An error occurred while retrieving tour details");
        }
    }

    /// <summary>
    /// Get all translations for a tour
    /// </summary>
    [HttpGet("{id}/translations")]
    public async Task<ActionResult<List<TourTranslationDto>>> GetTranslations(string id)
    {
        try
        {
            var tour = await _tourService.GetByIdAsync(id);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }

            var translations = await _tourService.GetTranslationsAsync(id);
            return Ok(translations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting translations for Tour {Id}", id);
            return StatusCode(500, "An error occurred while retrieving Tour translations");
        }
    }

    /// <summary>
    /// Create or update a translation for a tour
    /// </summary>
    [HttpPut("{id}/translations/{languageCode}")]
    public async Task<ActionResult<TourTranslationDto>> UpsertTranslation(string id, string languageCode, [FromBody] TourTranslationDto dto)
    {
        try
        {
            var (actorEmail, actorName) = GetActorIdentity();
            var translation = await _tourService.UpsertTranslationAsync(id, languageCode, dto);
            if (translation == null)
            {
                return NotFound($"Tour with ID '{id}' not found or translation input is invalid");
            }

            await _activityLogService.LogAsync(
                "Update",
                "TourTranslation",
                $"{id}:{translation.LanguageCode}",
                null,
                $"Cập nhật bản dịch {translation.LanguageCode} cho Tour ID: {id}",
                null,
                actorEmail,
                actorName);

            return Ok(translation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting translation for Tour {Id} language {LanguageCode}", id, languageCode);
            return StatusCode(500, "An error occurred while saving Tour translation");
        }
    }

    /// <summary>
    /// Delete a translation for a tour
    /// </summary>
    [HttpDelete("{id}/translations/{languageCode}")]
    public async Task<ActionResult> DeleteTranslation(string id, string languageCode)
    {
        try
        {
            var (actorEmail, actorName) = GetActorIdentity();
            var success = await _tourService.DeleteTranslationAsync(id, languageCode);
            if (!success)
            {
                return NotFound($"Translation '{languageCode}' for Tour '{id}' not found");
            }

            await _activityLogService.LogAsync(
                "Delete",
                "TourTranslation",
                $"{id}:{languageCode}",
                null,
                $"Xóa bản dịch {languageCode} của Tour ID: {id}",
                null,
                actorEmail,
                actorName);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting translation for Tour {Id} language {LanguageCode}", id, languageCode);
            return StatusCode(500, "An error occurred while deleting Tour translation");
        }
    }

    /// <summary>
    /// Create a new tour
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TourDto>> Create([FromBody] CreateTourDto dto)
    {
        try
        {
            var (actorEmail, actorName) = GetActorIdentity();
            var tour = await _tourService.CreateAsync(dto);
            await _activityLogService.LogAsync("Create", "Tour", tour.Id, tour.Name, $"Tạo Tour mới: {tour.Name}", null, actorEmail, actorName);
            return CreatedAtAction(nameof(GetById), new { id = tour.Id }, tour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tour");
            return StatusCode(500, "An error occurred while creating the tour");
        }
    }

    /// <summary>
    /// Update an existing tour
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TourDto>> Update(string id, [FromBody] UpdateTourDto dto)
    {
        try
        {
            var (actorEmail, actorName) = GetActorIdentity();
            var tour = await _tourService.UpdateAsync(id, dto);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Update", "Tour", id, tour.Name, $"Cập nhật Tour: {tour.Name}", null, actorEmail, actorName);
            return Ok(tour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tour {Id}", id);
            return StatusCode(500, "An error occurred while updating the tour");
        }
    }

    /// <summary>
    /// Delete a tour (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var (actorEmail, actorName) = GetActorIdentity();
            var success = await _tourService.DeleteAsync(id);
            if (!success)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Delete", "Tour", id, null, $"Xóa Tour ID: {id}", null, actorEmail, actorName);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tour {Id}", id);
            return StatusCode(500, "An error occurred while deleting the tour");
        }
    }

    /// <summary>
    /// Reorder POIs in a tour
    /// </summary>
    [HttpPut("{id}/reorder")]
    public async Task<ActionResult<TourDto>> ReorderPOIs(string id, [FromBody] List<string> poiIds)
    {
        try
        {
            var tour = await _tourService.ReorderPOIsAsync(id, poiIds);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            return Ok(tour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering POIs in tour {Id}", id);
            return StatusCode(500, "An error occurred while reordering POIs");
        }
    }

    private (string Email, string Name) GetActorIdentity()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;

        return (
            string.IsNullOrWhiteSpace(email) ? "system" : email,
            string.IsNullOrWhiteSpace(name) ? "Hệ thống" : name);
    }
}
