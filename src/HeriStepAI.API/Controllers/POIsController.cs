using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class POIsController : ControllerBase
{
    private readonly IPOIService _poiService;
    private readonly ILogger<POIsController> _logger;

    public POIsController(IPOIService poiService, ILogger<POIsController> logger)
    {
        _poiService = poiService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active POIs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<POIDto>>> GetAll([FromQuery] string? category = null)
    {
        try
        {
            var pois = await _poiService.GetAllAsync();
            
            if (!string.IsNullOrEmpty(category))
            {
                pois = pois.Where(p => p.Category == category).ToList();
            }

            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POIs");
            return StatusCode(500, "An error occurred while retrieving POIs");
        }
    }

    /// <summary>
    /// Get POI by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<POIDto>> GetById(string id)
    {
        try
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            return Ok(poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POI {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the POI");
        }
    }

    /// <summary>
    /// Get POIs near a location
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<List<POIDto>>> GetNearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 1000)
    {
        try
        {
            var pois = await _poiService.GetNearbyAsync(latitude, longitude, radiusMeters);
            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nearby POIs");
            return StatusCode(500, "An error occurred while retrieving nearby POIs");
        }
    }

    /// <summary>
    /// Create a new POI
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<POIDto>> Create([FromBody] CreatePOIDto dto)
    {
        try
        {
            var poi = await _poiService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating POI");
            return StatusCode(500, "An error occurred while creating the POI");
        }
    }

    /// <summary>
    /// Update an existing POI
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<POIDto>> Update(string id, [FromBody] UpdatePOIDto dto)
    {
        try
        {
            var poi = await _poiService.UpdateAsync(id, dto);
            if (poi == null)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            return Ok(poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating POI {Id}", id);
            return StatusCode(500, "An error occurred while updating the POI");
        }
    }

    /// <summary>
    /// Delete a POI (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var success = await _poiService.DeleteAsync(id);
            if (!success)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting POI {Id}", id);
            return StatusCode(500, "An error occurred while deleting the POI");
        }
    }

    /// <summary>
    /// Search POIs by name or description
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<POIDto>>> Search([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }

            var pois = await _poiService.SearchAsync(query);
            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching POIs");
            return StatusCode(500, "An error occurred while searching POIs");
        }
    }

    /// <summary>
    /// Get distinct POI categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        try
        {
            var pois = await _poiService.GetAllAsync();
            var categories = pois
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }
}
