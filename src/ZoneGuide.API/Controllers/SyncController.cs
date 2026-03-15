using ZoneGuide.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Sync content to mobile device - returns only updated content since last sync
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResponseDto>> Sync([FromBody] SyncRequestDto request)
    {
        try
        {
            var response = await _syncService.SyncDataAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data");
            return StatusCode(500, "An error occurred while syncing data");
        }
    }

    /// <summary>
    /// Get latest content version without full sync
    /// </summary>
    [HttpGet("version")]
    public async Task<ActionResult<ContentVersionDto>> GetVersion()
    {
        try
        {
            var version = await _syncService.GetLatestVersionAsync();
            return Ok(version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content version");
            return StatusCode(500, "An error occurred while retrieving content version");
        }
    }

    /// <summary>
    /// Full sync - get all content (for initial app setup)
    /// </summary>
    [HttpGet("full")]
    public async Task<ActionResult<SyncResponseDto>> FullSync(
        [FromQuery] bool includePOIs = true,
        [FromQuery] bool includeTours = true)
    {
        try
        {
            var request = new SyncRequestDto
            {
                LastSyncTime = null,
                IncludePOIs = includePOIs,
                IncludeTours = includeTours
            };

            var response = await _syncService.SyncDataAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing full sync");
            return StatusCode(500, "An error occurred while performing full sync");
        }
    }
}
