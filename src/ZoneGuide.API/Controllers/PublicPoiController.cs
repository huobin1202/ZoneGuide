using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using ZoneGuide.API.Services;

namespace ZoneGuide.API.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class PublicPoiController : Controller
{
    private const string QrDeviceCookieName = "zg_qr_device";
    private const int DeviceIdLength = 32;
    private const int FingerprintDeviceIdLength = 67; // fp- + 64 hex chars
    private readonly IQrRealtimeMonitoringService _qrMonitoringService;

    public PublicPoiController(IQrRealtimeMonitoringService qrMonitoringService)
    {
        _qrMonitoringService = qrMonitoringService;
    }

    [HttpGet("/poi/{id:int}")]
    public async Task<IActionResult> ShowPoi(int id, [FromQuery] bool autoplay = true)
    {
        var (deviceId, hasStableCookie) = EnsureDeviceIdCookie();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _qrMonitoringService.RegisterAccessAsync(id, deviceId, ipAddress, userAgent, hasStableCookie);

        return LocalRedirect($"/webapp/index.html?poiId={id}&autoplay={autoplay.ToString().ToLowerInvariant()}");
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
