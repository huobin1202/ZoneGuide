using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class POIsController : ControllerBase
{
    private readonly IPOIService _poiService;
    private readonly IActivityLogService _activityLogService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<POIsController> _logger;

    public POIsController(
        IPOIService poiService,
        IActivityLogService activityLogService,
        IHttpClientFactory httpClientFactory,
        ILogger<POIsController> logger)
    {
        _poiService = poiService;
        _activityLogService = activityLogService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get all POIs (Admin endpoint to get both active and inactive)
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<List<POIDto>>> GetAllAdmin([FromQuery] string? category = null)
    {
        try
        {
            var pois = await _poiService.GetAllAsync(includeInactive: true);

            if (!string.IsNullOrEmpty(category))
            {
                pois = pois.Where(p => p.Category == category).ToList();
            }

            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin POIs");
            return StatusCode(500, "An error occurred while retrieving POIs");
        }
    }

    /// <summary>
    /// Get all active POIs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<POIDto>>> GetAll([FromQuery] string? category = null)
    {
        try
        {
            var pois = await _poiService.GetAllAsync();
            
            if (!string.IsNullOrEmpty(category))
            {
                pois = pois.Where(p => p.Category == category).ToList();
            }

            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POIs");
            return StatusCode(500, "An error occurred while retrieving POIs");
        }
    }

    /// <summary>
    /// Get POI by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<POIDto>> GetById(string id)
    {
        try
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            return Ok(poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POI {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the POI");
        }
    }

    /// <summary>
    /// Get all translations for a POI
    /// </summary>
    [HttpGet("{id}/translations")]
    public async Task<ActionResult<List<POITranslationDto>>> GetTranslations(string id)
    {
        try
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null)
            {
                return NotFound($"POI with ID '{id}' not found");
            }

            var translations = await _poiService.GetTranslationsAsync(id);
            return Ok(translations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting translations for POI {Id}", id);
            return StatusCode(500, "An error occurred while retrieving POI translations");
        }
    }

    /// <summary>
    /// Create or update a translation for a POI
    /// </summary>
    [HttpPut("{id}/translations/{languageCode}")]
    public async Task<ActionResult<POITranslationDto>> UpsertTranslation(string id, string languageCode, [FromBody] POITranslationDto dto)
    {
        try
        {
            var translation = await _poiService.UpsertTranslationAsync(id, languageCode, dto);
            if (translation == null)
            {
                return NotFound($"POI with ID '{id}' not found or translation input is invalid");
            }

            await _activityLogService.LogAsync(
                "Update",
                "POITranslation",
                $"{id}:{translation.LanguageCode}",
                translation.Name,
                $"Cập nhật bản dịch {translation.LanguageCode} cho POI ID: {id}",
                null,
                "admin",
                "Admin");

            return Ok(translation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting translation for POI {Id} language {LanguageCode}", id, languageCode);
            return StatusCode(500, "An error occurred while saving POI translation");
        }
    }

    /// <summary>
    /// Delete a translation for a POI
    /// </summary>
    [HttpDelete("{id}/translations/{languageCode}")]
    public async Task<ActionResult> DeleteTranslation(string id, string languageCode)
    {
        try
        {
            var success = await _poiService.DeleteTranslationAsync(id, languageCode);
            if (!success)
            {
                return NotFound($"Translation '{languageCode}' for POI '{id}' not found");
            }

            await _activityLogService.LogAsync(
                "Delete",
                "POITranslation",
                $"{id}:{languageCode}",
                null,
                $"Xóa bản dịch {languageCode} của POI ID: {id}",
                null,
                "admin",
                "Admin");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting translation for POI {Id} language {LanguageCode}", id, languageCode);
            return StatusCode(500, "An error occurred while deleting POI translation");
        }
    }

    /// <summary>
    /// Auto translate POI content to target language.
    /// </summary>
    [HttpPost("translate-content")]
    public async Task<ActionResult<TranslatedPOIContentDto>> TranslateContent([FromBody] TranslatePOIContentRequestDto request)
    {
        try
        {
            if (request is null)
            {
                return BadRequest("Nội dung dịch không hợp lệ");
            }

            if (string.IsNullOrWhiteSpace(request.TargetLanguage))
            {
                return BadRequest("Thiếu ngôn ngữ đích");
            }

            var sourceLanguage = NormalizeLanguageCode(request.SourceLanguage);
            var targetLanguage = NormalizeLanguageCode(request.TargetLanguage);

            if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new TranslatedPOIContentDto
                {
                    Name = request.Name,
                    TTSScript = request.TTSScript
                });
            }

            var translatedTtsTask = TranslateTextSafelyAsync(request.TTSScript, sourceLanguage, targetLanguage);
            await translatedTtsTask;

            return Ok(new TranslatedPOIContentDto
            {
                // Always keep original place name; only narration content is translated.
                Name = request.Name,
                TTSScript = translatedTtsTask.Result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-translating POI content from {SourceLanguage} to {TargetLanguage}", request?.SourceLanguage, request?.TargetLanguage);
            return StatusCode(500, "Có lỗi khi dịch nội dung");
        }
    }

    /// <summary>
    /// Get POIs near a location
    /// </summary>
    [HttpGet("nearby")]
    public async Task<ActionResult<List<POIDto>>> GetNearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 1000)
    {
        try
        {
            var pois = await _poiService.GetNearbyAsync(latitude, longitude, radiusMeters);
            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nearby POIs");
            return StatusCode(500, "An error occurred while retrieving nearby POIs");
        }
    }

    /// <summary>
    /// Create a new POI
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<POIDto>> Create([FromBody] CreatePOIDto dto)
    {
        try
        {
            var poi = await _poiService.CreateAsync(dto);
            await _activityLogService.LogAsync("Create", "POI", poi.Id, poi.Name, $"Tạo POI mới: {poi.Name}", null, "admin", "Admin");
            return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating POI");
            return StatusCode(500, "An error occurred while creating the POI");
        }
    }

    /// <summary>
    /// Update an existing POI
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<POIDto>> Update(string id, [FromBody] UpdatePOIDto dto)
    {
        try
        {
            var poi = await _poiService.UpdateAsync(id, dto);
            if (poi == null)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Update", "POI", id, poi.Name, $"Cập nhật POI: {poi.Name}", null, "admin", "Admin");
            return Ok(poi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating POI {Id}", id);
            return StatusCode(500, "An error occurred while updating the POI");
        }
    }

    /// <summary>
    /// Delete a POI (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var success = await _poiService.DeleteAsync(id);
            if (!success)
            {
                return NotFound($"POI with ID '{id}' not found");
            }
            await _activityLogService.LogAsync("Delete", "POI", id, null, $"Xóa POI ID: {id}", null, "admin", "Admin");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting POI {Id}", id);
            return StatusCode(500, "An error occurred while deleting the POI");
        }
    }

    /// <summary>
    /// Search POIs by name or description
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<POIDto>>> Search([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }

            var pois = await _poiService.SearchAsync(query);
            return Ok(pois);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching POIs");
            return StatusCode(500, "An error occurred while searching POIs");
        }
    }

    /// <summary>
    /// Get distinct POI categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        try
        {
            var pois = await _poiService.GetAllAsync();
            var categories = pois
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    private async Task<string> TranslateTextSafelyAsync(string? text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        try
        {
            var translated = await TranslateTextWithGoogleAsync(text, sourceLanguage, targetLanguage);
            return string.IsNullOrWhiteSpace(translated) ? text : translated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Translate failed, fallback to source text");
            return text;
        }
    }

    private async Task<string?> TranslateTextWithGoogleAsync(string text, string sourceLanguage, string targetLanguage)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(20);

        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={Uri.EscapeDataString(sourceLanguage)}&tl={Uri.EscapeDataString(targetLanguage)}&dt=t&q={Uri.EscapeDataString(text)}";
        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            return null;

        var segments = document.RootElement[0];
        if (segments.ValueKind != JsonValueKind.Array)
            return null;

        var builder = new StringBuilder();
        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0)
            {
                var translatedPart = segment[0].GetString();
                if (!string.IsNullOrEmpty(translatedPart))
                {
                    builder.Append(translatedPart);
                }
            }
        }

        return builder.ToString();
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "auto";

        var trimmed = languageCode.Trim();
        if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
            return "auto";

        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex > 0)
        {
            return trimmed[..dashIndex].ToLowerInvariant();
        }

        return trimmed.ToLowerInvariant();
    }
}
