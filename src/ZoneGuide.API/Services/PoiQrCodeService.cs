using QRCoder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ZoneGuide.API.Services;

public class PoiQrCodeService
{
    public const string PayloadPrefix = "POI:";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PoiQrCodeService> _logger;
    private readonly QRCodeGenerator _qrGenerator = new();

    public PoiQrCodeService(IWebHostEnvironment env, ILogger<PoiQrCodeService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string BuildPayload(int poiId) => $"{PayloadPrefix}{poiId}";

    // Public URL served from wwwroot
    public string GetQrUrl(int poiId) => $"/uploads/qrcodes/poi-{poiId}.png";

    private string GetQrFilePath(int poiId)
    {
        // WebRootPath can be null in rare hosting scenarios.
        var webRoot = _env.WebRootPath ?? string.Empty;
        return Path.Combine(webRoot, "uploads", "qrcodes", $"poi-{poiId}.png");
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
        if (!force && File.Exists(filePath))
            return true;

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
        _logger.LogInformation("Generated QR for POI {PoiId} -> {FilePath}", poiId, filePath);

        return true;
    }
}

