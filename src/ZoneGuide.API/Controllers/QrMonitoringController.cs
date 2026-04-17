using Microsoft.AspNetCore.Mvc;
using ZoneGuide.API.Services;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/qr-monitoring")]
public class QrMonitoringController : ControllerBase
{
    private readonly IQrRealtimeMonitoringService _monitoringService;

    public QrMonitoringController(IQrRealtimeMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpGet("snapshot")]
    public ActionResult<QrMonitoringSnapshotDto> GetSnapshot()
    {
        return Ok(_monitoringService.GetSnapshot());
    }
}
