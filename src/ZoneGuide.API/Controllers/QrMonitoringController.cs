using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using ZoneGuide.API.Services;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/qr-monitoring")]
public class QrMonitoringController : ControllerBase
{
    private const string QrDeviceCookieName = "zg_qr_device";
    private const int DeviceIdLength = 32;
    private const int FingerprintDeviceIdLength = 67;
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

    [HttpPost("presence")]
    public async Task<ActionResult<QrMonitoringSnapshotDto>> RegisterPresence([FromBody] QrPresenceHeartbeatRequest heartbeat)
    {
        if (heartbeat == null || heartbeat.PoiId <= 0 || string.IsNullOrWhiteSpace(heartbeat.SessionId))
        {
            return BadRequest();
        }

        var (deviceId, hasStableCookie) = EnsureDeviceIdCookie();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var snapshot = await _monitoringService.RegisterPresenceAsync(
            heartbeat.PoiId,
            heartbeat.SessionId,
            deviceId,
            ipAddress,
            userAgent,
            hasStableCookie);

        return Ok(snapshot);
    }

    [HttpPost("presence/stop")]
    public async Task<ActionResult<QrMonitoringSnapshotDto>> StopPresence([FromBody] QrPresenceHeartbeatRequest heartbeat)
    {
        if (heartbeat == null || string.IsNullOrWhiteSpace(heartbeat.SessionId))
        {
            return BadRequest();
        }

        var snapshot = await _monitoringService.UnregisterPresenceAsync(heartbeat.SessionId);
        return Ok(snapshot);
    }

    private (string DeviceId, bool HasStableCookie) EnsureDeviceIdCookie()
    {
        if (Request.Cookies.TryGetValue(QrDeviceCookieName, out var existing) &&
            IsValidDeviceId(existing))
        {
            return (existing, true);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var created = BuildDeterministicDeviceId(ipAddress, userAgent) ?? Guid.NewGuid().ToString("N");

        Response.Cookies.Append(QrDeviceCookieName, created, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        return (created, true);
    }

    private static bool IsValidDeviceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length == DeviceIdLength && Guid.TryParseExact(normalized, "N", out _))
        {
            return true;
        }

        return normalized.Length == FingerprintDeviceIdLength && normalized.StartsWith("fp-", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildDeterministicDeviceId(string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var fingerprint = string.Concat(ipAddress.Trim(), "|", userAgent.Trim());
        if (fingerprint.Length <= 1)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return "fp-" + Convert.ToHexString(bytes);
    }
}

public sealed class QrPresenceHeartbeatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public int PoiId { get; set; }
}
