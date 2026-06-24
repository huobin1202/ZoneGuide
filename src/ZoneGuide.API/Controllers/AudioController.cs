using Microsoft.AspNetCore.Mvc;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AudioController> _logger;
    private static readonly string AudioOutputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio", "generated");

    public AudioController(IHttpClientFactory httpClientFactory, ILogger<AudioController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        if (!Directory.Exists(AudioOutputDir))
        {
            Directory.CreateDirectory(AudioOutputDir);
        }
    }

    /// <summary>
    /// Generate TTS audio using Google Translate TTS (free, no API key).
    /// POST /api/audio/generate-tts
    /// </summary>
    [HttpPost("generate-tts")]
    public async Task<ActionResult<GenerateTtsResponse>> GenerateTts([FromBody] GenerateTtsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new GenerateTtsResponse { Success = false, Error = "Text is required" });
        }

        var text = request.Text.Trim();
        var lang = NormalizeTtsLanguage(request.Language ?? "vi");
        var maxBytes = 200 * 1024; // ~200KB limit for safety
        var textSample = text.Length > 200 ? text[..200] : text;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Google Translate TTS endpoint - completely free, no API key
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(textSample)}&tl={lang}&client=tw-ob";

            using var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google TTS returned {StatusCode} for lang={Lang}", response.StatusCode, lang);
                return StatusCode(502, new GenerateTtsResponse { Success = false, Error = "TTS service unavailable" });
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync();
            if (audioBytes.Length == 0 || audioBytes.Length > maxBytes)
            {
                // Text too long - split and concatenate could be done but for simplicity
                // generate only the first 200 chars. Full text will be handled by browser TTS.
            }

            // If text is longer, try the full text (Google TTS supports longer texts)
            if (text.Length > 200)
            {
                // Try full text
                var fullUrl = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(text)}&tl={lang}&client=tw-ob";
                using var fullResponse = await httpClient.GetAsync(fullUrl);
                if (fullResponse.IsSuccessStatusCode)
                {
                    var fullBytes = await fullResponse.Content.ReadAsByteArrayAsync();
                    if (fullBytes.Length > 0 && fullBytes.Length <= maxBytes * 3)
                    {
                        audioBytes = fullBytes;
                    }
                }
            }

            // Save file
            var fileId = Guid.NewGuid().ToString("N")[..12];
            var safeLang = lang.Replace('-', '_');
            var fileName = $"tts_{fileId}_{safeLang}.mp3";
            var filePath = Path.Combine(AudioOutputDir, fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, audioBytes);

            var relativeUrl = $"/uploads/audio/generated/{fileName}";
            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

            _logger.LogInformation("Generated TTS audio: {FileName} ({Length} bytes)", fileName, audioBytes.Length);

            return Ok(new GenerateTtsResponse
            {
                Success = true,
                AudioUrl = absoluteUrl,
                AudioPath = relativeUrl,
                FileName = fileName,
                ContentLength = audioBytes.Length
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call Google TTS service");
            return StatusCode(502, new GenerateTtsResponse { Success = false, Error = "Failed to reach TTS service" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS generation failed");
            return StatusCode(500, new GenerateTtsResponse { Success = false, Error = "Internal error generating audio" });
        }
    }

    private static string NormalizeTtsLanguage(string language)
    {
        // Map language codes to Google TTS codes
        var lang = language.Trim().Replace('_', '-').ToLowerInvariant();

        return lang switch
        {
            var l when l.StartsWith("vi") => "vi",
            var l when l.StartsWith("en") => "en",
            var l when l.StartsWith("ja") => "ja",
            var l when l.StartsWith("ko") => "ko",
            var l when l.StartsWith("zh") => "zh-CN",
            var l when l.StartsWith("fr") => "fr",
            var l when l.StartsWith("de") => "de",
            var l when l.StartsWith("th") => "th",
            var l when l.StartsWith("es") => "es",
            var l when l.StartsWith("ru") => "ru",
            _ => "en"
        };
    }
}
