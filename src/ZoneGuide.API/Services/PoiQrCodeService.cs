using QRCoder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ZoneGuide.API.Services;

public class PoiQrCodeService
{
    private const string DefaultPublicBaseUrl = "https://localhost:56040";
    private const string TunnelUrlEnvVar = "ZONEGUIDE_PUBLIC_TUNNEL_URL";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PoiQrCodeService> _logger;
    private readonly string _publicBaseUrl;
    private readonly QRCodeGenerator _qrGenerator = new();

    public PoiQrCodeService(
        IWebHostEnvironment env,
        ILogger<PoiQrCodeService> logger,
        IConfiguration configuration)
    {
        _env = env;
        _logger = logger;

        var lanBaseUrl = NormalizeBaseUrl(configuration["PublicWebApp:BaseUrl"]);
        var tunnelEnvBaseUrl = NormalizeBaseUrl(Environment.GetEnvironmentVariable(TunnelUrlEnvVar));
        var tunnelBaseUrl = tunnelEnvBaseUrl ?? NormalizeBaseUrl(configuration["PublicWebApp:TunnelBaseUrl"]);
        var preferTunnel = configuration.GetValue<bool>("PublicWebApp:PreferTunnel");

        _publicBaseUrl = ResolvePublicBaseUrl(lanBaseUrl, tunnelBaseUrl, preferTunnel, tunnelEnvBaseUrl != null);
        _logger.LogInformation("QR payload base URL: {BaseUrl}", _publicBaseUrl);
    }

    private static string ResolvePublicBaseUrl(string? lanBaseUrl, string? tunnelBaseUrl, bool preferTunnel, bool hasTunnelEnvOverride)
    {
        if (hasTunnelEnvOverride && !string.IsNullOrWhiteSpace(tunnelBaseUrl))
        {
            return tunnelBaseUrl;
        }

        if (preferTunnel && !string.IsNullOrWhiteSpace(tunnelBaseUrl))
        {
            return tunnelBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(lanBaseUrl))
        {
            return lanBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(tunnelBaseUrl))
        {
            return tunnelBaseUrl;
        }

        return DefaultPublicBaseUrl;
    }

    private static string? NormalizeBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed.TrimEnd('/');
    }

    public string BuildPayload(int poiId) => GetPoiLandingUrl(poiId);

    public string GetPoiLandingPath(int poiId) => $"/poi/{poiId}?autoplay=true";

    public string GetPoiLandingUrl(int poiId) => $"{_publicBaseUrl}{GetPoiLandingPath(poiId)}";

    // Public URL served from wwwroot
    public string GetQrUrl(int poiId) => $"/uploads/qrcodes/poi-{poiId}.png";

    private string GetQrFilePath(int poiId)
    {
        // WebRootPath can be null in rare hosting scenarios.
        var webRoot = _env.WebRootPath ?? string.Empty;
        return Path.Combine(webRoot, "uploads", "qrcodes", $"poi-{poiId}.png");
    }

    private string GetPayloadFilePath(int poiId)
    {
        var webRoot = _env.WebRootPath ?? string.Empty;
        return Path.Combine(webRoot, "uploads", "qrcodes", $"poi-{poiId}.payload.txt");
    }

    public bool QrExists(int poiId)
    {
        var path = GetQrFilePath(poiId);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public async Task<bool> EnsureQrCodeGeneratedAsync(int poiId, bool force = false)
    {
        var payload = BuildPayload(poiId);
        return await EnsureQrCodeGeneratedAsync(poiId, payload, force);
    }

    private async Task<bool> EnsureQrCodeGeneratedAsync(int poiId, string payload, bool force)
    {
        var filePath = GetQrFilePath(poiId);
        var payloadFilePath = GetPayloadFilePath(poiId);

        if (!force && File.Exists(filePath) && File.Exists(payloadFilePath))
        {
            try
            {
                var existingPayload = await File.ReadAllTextAsync(payloadFilePath);
                if (string.Equals(existingPayload.Trim(), payload, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            catch
            {
                // If payload marker is unreadable, regenerate defensively.
            }
        }

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // ECC level Q gives good balance between size and robustness.
        using var qrData = _qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(qrData);
        var bytes = png.GetGraphic(20);

        await File.WriteAllBytesAsync(filePath, bytes);
        await File.WriteAllTextAsync(payloadFilePath, payload);
        _logger.LogInformation("Generated QR for POI {PoiId} -> {FilePath}", poiId, filePath);

        return true;
    }
}

