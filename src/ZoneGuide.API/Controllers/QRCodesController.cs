using Microsoft.AspNetCore.Mvc;
using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QRCodesController : ControllerBase
{
    private readonly PoiQrCodeService _qrCodeService;
    private readonly IPOIService _poiService;

    public QRCodesController(PoiQrCodeService qrCodeService, IPOIService poiService)
    {
        _qrCodeService = qrCodeService;
        _poiService = poiService;
    }

    [HttpGet("pois")]
    public async Task<ActionResult<List<PoiQrCodeDto>>> GetPoiQRCodes([FromQuery] bool includeInactive = false)
    {
        var pois = await _poiService.GetAllAsync(includeInactive: includeInactive);
        var result = new List<PoiQrCodeDto>(pois.Count);

        foreach (var poi in pois)
        {
            if (!int.TryParse(poi.Id, out var poiId))
                continue;

            result.Add(new PoiQrCodeDto
            {
                PoiId = poiId,
                Name = poi.Name,
                Payload = _qrCodeService.BuildPayload(poiId),
                QrUrl = _qrCodeService.GetQrUrl(poiId),
                Exists = _qrCodeService.QrExists(poiId)
            });
        }

        return Ok(result.OrderBy(x => x.PoiId).ToList());
    }

    [HttpPost("pois/{id}/generate")]
    public async Task<ActionResult<PoiQrCodeDto>> GeneratePoiQrCode(string id, [FromQuery] bool force = false)
    {
        if (!int.TryParse(id, out var poiId))
            return BadRequest("Invalid POI id.");

        var poi = await _poiService.GetByIdAsync(poiId.ToString());
        if (poi == null)
            return NotFound($"POI with ID '{id}' not found");

        await _qrCodeService.EnsureQrCodeGeneratedAsync(poiId, force: force);

        return Ok(new PoiQrCodeDto
        {
            PoiId = poiId,
            Name = poi.Name,
            Payload = _qrCodeService.BuildPayload(poiId),
            QrUrl = _qrCodeService.GetQrUrl(poiId),
            Exists = true
        });
    }

    [HttpPost("pois/generate-missing")]
    public async Task<ActionResult<int>> GenerateMissingPoiQRCodes([FromQuery] bool includeInactive = false, [FromQuery] bool force = false)
    {
        var pois = await _poiService.GetAllAsync(includeInactive: includeInactive);
        var generatedCount = 0;

        foreach (var poi in pois)
        {
            if (!int.TryParse(poi.Id, out var poiId))
                continue;

            if (!force && _qrCodeService.QrExists(poiId))
                continue;

            await _qrCodeService.EnsureQrCodeGeneratedAsync(poiId, force: force);
            generatedCount++;
        }

        return Ok(generatedCount);
    }
}

