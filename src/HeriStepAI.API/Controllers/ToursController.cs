using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

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
    /// Create a new tour
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TourDto>> Create([FromBody] CreateTourDto dto)
    {
        try
        {
            var tour = await _tourService.CreateAsync(dto);
            await _activityLogService.LogAsync("Create", "Tour", tour.Id, tour.Name, $"Tạo Tour mới: {tour.Name}", null, "admin", "Admin");
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
            var tour = await _tourService.UpdateAsync(id, dto);
            if (tour == null)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Update", "Tour", id, tour.Name, $"Cập nhật Tour: {tour.Name}", null, "admin", "Admin");
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
            var success = await _tourService.DeleteAsync(id);
            if (!success)
            {
                return NotFound($"Tour with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Delete", "Tour", id, null, $"Xóa Tour ID: {id}", null, "admin", "Admin");
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
}
