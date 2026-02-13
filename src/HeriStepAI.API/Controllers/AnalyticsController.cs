using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Upload analytics data from mobile devices
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult> Upload([FromBody] AnalyticsUploadDto data)
    {
        try
        {
            if (string.IsNullOrEmpty(data.AnonymousDeviceId))
            {
                return BadRequest("AnonymousDeviceId is required");
            }

            await _analyticsService.UploadAnalyticsAsync(data);
            return Ok(new { message = "Analytics uploaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading analytics");
            return StatusCode(500, "An error occurred while uploading analytics");
        }
    }

    /// <summary>
    /// Get dashboard analytics (for admin portal)
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardAnalyticsDto>> GetDashboard(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var dashboard = await _analyticsService.GetDashboardAsync(from, to);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard analytics");
            return StatusCode(500, "An error occurred while retrieving dashboard analytics");
        }
    }

    /// <summary>
    /// Get top POIs by listen count
    /// </summary>
    [HttpGet("top-pois")]
    public async Task<ActionResult<List<TopPOIDto>>> GetTopPOIs(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int count = 10)
    {
        try
        {
            var topPOIs = await _analyticsService.GetTopPOIsAsync(from, to, count);
            return Ok(topPOIs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top POIs");
            return StatusCode(500, "An error occurred while retrieving top POIs");
        }
    }

    /// <summary>
    /// Get heatmap data for visitor locations
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<ActionResult<List<HeatmapPointDto>>> GetHeatmap(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var heatmapData = await _analyticsService.GetHeatmapDataAsync(from, to);
            return Ok(heatmapData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting heatmap data");
            return StatusCode(500, "An error occurred while retrieving heatmap data");
        }
    }
}
