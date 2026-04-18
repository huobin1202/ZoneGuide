using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/mobile-monitoring")]
public class MobileMonitoringController : ControllerBase
{
    private readonly IMobileLiveMonitoringService _monitoringService;

    public MobileMonitoringController(IMobileLiveMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<MobileLiveMonitoringSnapshotDto>> RegisterHeartbeat([FromBody] MobileLiveHeartbeatDto heartbeat)
    {
        if (heartbeat == null)
        {
            return BadRequest();
        }

        var userId = GetCurrentUserId();
        var userDisplayName = User.FindFirstValue(ClaimTypes.Name);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);

        var snapshot = await _monitoringService.RegisterHeartbeatAsync(heartbeat, userId, userDisplayName, userEmail);
        return Ok(snapshot);
    }

    [HttpGet("snapshot")]
    public ActionResult<MobileLiveMonitoringSnapshotDto> GetSnapshot()
    {
        return Ok(_monitoringService.GetSnapshot());
    }

    private int? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var userId) ? userId : null;
    }
}
