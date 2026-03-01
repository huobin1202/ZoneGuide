using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityLogController : ControllerBase
{
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<ActivityLogController> _logger;

    public ActivityLogController(IActivityLogService activityLogService, ILogger<ActivityLogController> logger)
    {
        _activityLogService = activityLogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var (items, totalCount) = await _activityLogService.GetLogsAsync(page, pageSize, entityType, action, from, to);
            return Ok(new { items, totalCount, page, pageSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity logs");
            return StatusCode(500, "Error retrieving activity logs");
        }
    }
}
