using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZoneGuide.API.Data;
using ZoneGuide.API.Services;

namespace ZoneGuide.API.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class PublicPoiController : Controller
{
    private const string QrDeviceCookieName = "zg_qr_device";
    private const int DeviceIdLength = 32;
    private const int FingerprintDeviceIdLength = 67;
    private readonly IQrRealtimeMonitoringService _qrMonitoringService;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public PublicPoiController(IQrRealtimeMonitoringService qrMonitoringService, AppDbContext db, IConfiguration configuration)
    {
        _qrMonitoringService = qrMonitoringService;
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("/poi/{id:int}")]
    public async Task<IActionResult> ShowPoi(int id, [FromQuery] bool autoplay = true)
    {
        var (deviceId, hasStableCookie) = EnsureDeviceIdCookie();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        await _qrMonitoringService.RegisterAccessAsync(id, deviceId, ipAddress, userAgent, hasStableCookie);

        var poi = await _db.POIs
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (poi is null)
            return Content(ErrorPage("Không tìm thấy"), "text/html");

        var guestBaseUrl = _configuration["PublicWebApp:AdminBaseUrl"] ?? "http://192.168.2.119:56041";
        var encodedName = WebUtility.HtmlEncode(poi.Name);
        var encodedAddress = WebUtility.HtmlEncode(poi.Address ?? "");
        var encodedCategory = WebUtility.HtmlEncode(poi.Category ?? "");
        var encodedTts = WebUtility.HtmlEncode(poi.TTSScript ?? "");
        var encodedAudioUrl = WebUtility.HtmlEncode(poi.AudioUrl ?? "");
        var safeNameJs = encodedName.Replace("'", "\\'");
        var safeAudioUrlJs = encodedAudioUrl.Replace("'", "\\'");
        var safeTtsJs = WebUtility.HtmlEncode(poi.TTSScript ?? "").Replace("'", "\\'");

        var imageStyle = !string.IsNullOrEmpty(poi.ImageUrl)
            ? $"background-image:url('{poi.ImageUrl}')"
            : "background:linear-gradient(135deg,#1976D2,#1565C0)";

        var categoryBadge = !string.IsNullOrEmpty(poi.Category)
            ? $"<span class=\"category-badge\">{encodedCategory}</span>"
            : "";

        var translationsJson = JsonSerializer.Serialize(poi.Translations.Select(t => new
        {
            t.LanguageCode, t.Name, t.TTSScript, t.AudioUrl
        }));

        var translationsChips = string.Join("", poi.Translations.Select(t =>
            $"<span class=\"lang-chip\" onclick=\"switchLang('{t.LanguageCode}')\">{t.LanguageCode}</span>"
        ));

        var translationsSection = poi.Translations.Count > 0
            ? $"<div class=\"section\"><div class=\"section-title\">Ngôn ngữ</div><div class=\"lang-chips\">{translationsChips}</div></div>"
            : "";

        var audioSection = !string.IsNullOrEmpty(poi.AudioUrl) || !string.IsNullOrEmpty(poi.TTSScript)
            ? $"<div class=\"section\"><div class=\"section-title\">Giới thiệu</div><button class=\"play-btn\" onclick=\"playAudio()\"><span class=\"play-icon\">&#9654;</span> Nghe giới thiệu</button><div class=\"tts-script\">{encodedTts}</div></div>"
            : "";

        var autoPlayScript = autoplay && !string.IsNullOrEmpty(poi.AudioUrl)
            ? "setTimeout(function(){if(poiData.audioUrl)playAudio();},500)"
            : "";

        var html = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no""/>
<meta name=""theme-color"" content=""#1976D2""/>
<title>{encodedName} - ZoneGuide</title>
<style>
*{{margin:0;padding:0;box-sizing:border-box}}
body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f6f7fb;color:#333;padding-bottom:80px}}
.top-bar{{display:flex;align-items:center;padding:8px 16px;background:white;border-bottom:1px solid #f0f0f0;position:sticky;top:0;z-index:50}}
.back-btn{{display:flex;align-items:center;gap:4px;color:#1976D2;text-decoration:none;font-size:14px;font-weight:500;padding:4px 0}}
.hero{{width:100%;height:220px;{imageStyle};background-size:cover;background-position:center;position:relative}}
.hero-overlay{{position:absolute;bottom:0;left:0;right:0;padding:16px 20px;background:linear-gradient(transparent,rgba(0,0,0,.7));color:#fff}}
.hero-name{{font-size:22px;font-weight:700;margin-bottom:2px}}
.hero-address{{font-size:13px;opacity:.85;margin-top:4px}}
.category-badge{{display:inline-block;background:rgba(255,255,255,.2);padding:2px 10px;border-radius:12px;font-size:12px;margin-bottom:6px}}
.content{{padding:16px}}
.section{{margin-bottom:20px}}
.section-title{{font-size:15px;font-weight:600;margin-bottom:8px;color:#444}}
.play-btn{{display:flex;align-items:center;gap:8px;width:100%;padding:12px 16px;background:#1976D2;color:#fff;border:none;border-radius:12px;font-size:15px;font-weight:500;cursor:pointer}}
.play-btn:active{{background:#1565C0}}
.play-icon{{font-size:18px}}
.tts-script{{margin-top:12px;line-height:1.6;color:#555;font-size:14px}}
.lang-chips{{display:flex;gap:6px;flex-wrap:wrap}}
.lang-chip{{padding:4px 12px;background:#e3f2fd;color:#1976D2;border-radius:16px;font-size:12px;cursor:pointer}}
.lang-chip:active{{background:#bbdefb}}
.info-card{{background:#f8f9fa;border-radius:12px;padding:12px 16px}}
.info-row{{display:flex;justify-content:space-between;padding:4px 0;font-size:13px}}
.info-label{{color:#999}}
.info-value{{font-weight:500}}
.web-link{{display:block;text-align:center;padding:12px;margin:16px;background:white;border-radius:12px;color:#1976D2;text-decoration:none;font-weight:500;box-shadow:0 1px 4px rgba(0,0,0,.08)}}
#audio-player{{position:fixed;bottom:60px;left:0;right:0;background:#fff;border-top:1px solid #e0e0e0;padding:8px 16px;display:none;box-shadow:0 -4px 12px rgba(0,0,0,.1);z-index:100}}
#audio-player.show{{display:flex;align-items:center;gap:12px}}
#audio-player audio{{flex:1;height:36px}}
.close-btn{{background:none;border:none;font-size:20px;cursor:pointer;color:#999;padding:4px}}
.bottom-nav{{position:fixed;bottom:0;left:0;right:0;background:white;display:flex;justify-content:space-around;align-items:center;height:60px;padding-bottom:env(safe-area-inset-bottom,4px);box-shadow:0 -2px 10px rgba(0,0,0,.1);z-index:101}}
.nav-item{{display:flex;flex-direction:column;align-items:center;color:#999;padding:4px 8px;font-size:10px;gap:2px;text-decoration:none}}
.nav-item svg{{width:22px;height:22px;fill:currentColor}}
@media(prefers-color-scheme:dark){{body{{background:#121212;color:#e0e0e0}}.top-bar{{background:#1e1e1e;border-color:#333}}.section-title{{color:#aaa}}.tts-script{{color:#bbb}}.lang-chip{{background:#1e3a5f;color:#90cafc}}.web-link{{background:#1e1e1e;color:#90caf9}}#audio-player{{background:#1e1e1e;border-color:#333}}.info-card{{background:#1e1e1e}}.bottom-nav{{background:#1e1e1e;border-color:#333}}.nav-item{{color:#888}}.back-btn{{color:#90caf9}}}}
</style>
</head>
<body>
<div class=""top-bar"">
<a href=""javascript:history.back()"" class=""back-btn"">&#8592; Quay lại</a>
</div>
<div class=""hero"">
<div class=""hero-overlay"">
{categoryBadge}
<div class=""hero-name"">{encodedName}</div>
<div class=""hero-address"">{encodedAddress}</div>
</div>
</div>
<div class=""content"">
{audioSection}
{translationsSection}
<div class=""section"">
<div class=""section-title"">Thông tin</div>
<div class=""info-card"">
<div class=""info-row""><span class=""info-label"">Khoảng cách</span><span class=""info-value"">~{poi.TriggerRadius}m</span></div>
<div class=""info-row""><span class=""info-label"">Ngôn ngữ</span><span class=""info-value"">{poi.Language}</span></div>
</div>
</div>
</div>
<a class=""web-link"" href=""{guestBaseUrl}/poi/{id}"" target=""_blank"">M&#7903; trong &#7913;ng d&#7909;ng web &#8594;</a>
<div id=""audio-player"">
<audio controls src="""" id=""audio-el"" style=""flex:1""></audio>
<button class=""close-btn"" onclick=""closePlayer()"">&#10005;</button>
</div>
<nav class=""bottom-nav"">
<a href=""/home"" class=""nav-item"">
<svg viewBox=""0 0 24 24""><path d=""M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z""/></svg>
<span>Trang ch&#7911;</span>
</a>
<a href=""/map"" class=""nav-item"">
<svg viewBox=""0 0 24 24""><path d=""M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z""/></svg>
<span>B&#7843;n &#273;&#7891;</span>
</a>
<a href=""/poi-list"" class=""nav-item"">
<svg viewBox=""0 0 24 24""><path d=""M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z""/></svg>
<span>&#272;&#7883;a &#273;i&#7875;m</span>
</a>
<a href=""/tour-list"" class=""nav-item"">
<svg viewBox=""0 0 24 24""><path d=""M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z""/></svg>
<span>Tour</span>
</a>
<a href=""/more"" class=""nav-item"">
<svg viewBox=""0 0 24 24""><path d=""M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z""/></svg>
<span>Th&#234;m</span>
</a>
</nav>
<script>
var poiData={{id:{id},name:'{safeNameJs}',audioUrl:'{safeAudioUrlJs}',ttsScript:'{safeTtsJs}',currentLang:'vi',translations:{translationsJson}}};
function saveHistory(){{try{{var h=JSON.parse(localStorage.getItem('zg_history'))||[];h=h.filter(function(e){{return!(e.id===poiData.id&&e.timestamp===new Date().toISOString().slice(0,16))}});h.unshift({{id:poiData.id,name:poiData.name,category:'{encodedCategory}',imageUrl:'{WebUtility.HtmlEncode(poi.ImageUrl??"")}',timestamp:new Date().toISOString(),durationSeconds:0,language:poiData.currentLang||'vi',playCount:1}});if(h.length>200)h.length=200;localStorage.setItem('zg_history',JSON.stringify(h))}}catch(e){{}}}}
function playAudio(){{saveHistory();var p=document.getElementById('audio-player'),a=document.getElementById('audio-el');p.classList.add('show');a.src=poiData.audioUrl;a.play()}}
function closePlayer(){{var p=document.getElementById('audio-player'),a=document.getElementById('audio-el');p.classList.remove('show');a.pause();a.src=''}}
function switchLang(l){{var t=poiData.translations.find(function(x){{return x.LanguageCode===l}});if(!t)return;poiData.currentLang=l;var n=document.querySelector('.hero-name'),s=document.querySelector('.tts-script'),c=document.querySelectorAll('.lang-chip');c.forEach(function(x){{x.style.background=x.textContent===l?'#bbdefb':'#e3f2fd'}});if(t.Name)n.textContent=t.Name;if(t.TTSScript&&s)s.textContent=t.TTSScript;if(t.AudioUrl)poiData.audioUrl=t.AudioUrl}}
{autoPlayScript}
</script>
</body>
</html>";

        return Content(html, "text/html", Encoding.UTF8);
    }

    private static string ErrorPage(string message)
    {
        return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>{WebUtility.HtmlEncode(message)}</title></head>
<body style=""font-family:sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;color:#999;text-align:center;padding:20px"">
<h2>{WebUtility.HtmlEncode(message)}</h2></body></html>";
    }

    private (string DeviceId, bool HasStableCookie) EnsureDeviceIdCookie()
    {
        if (Request.Cookies.TryGetValue(QrDeviceCookieName, out var existing) &&
            IsValidDeviceId(existing))
            return (existing, true);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var created = BuildDeterministicDeviceId(ipAddress, userAgent) ?? Guid.NewGuid().ToString("N");

        Response.Cookies.Append(QrDeviceCookieName, created, new CookieOptions
        {
            HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax,
            Path = "/", Secure = Request.IsHttps, Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
        return (created, true);
    }

    private static bool IsValidDeviceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        return (normalized.Length == DeviceIdLength && Guid.TryParseExact(normalized, "N", out _)) ||
               (normalized.Length == FingerprintDeviceIdLength && normalized.StartsWith("fp-", StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildDeterministicDeviceId(string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent)) return null;
        var fingerprint = string.Concat(ipAddress.Trim(), "|", userAgent.Trim());
        if (fingerprint.Length <= 1) return null;
        return "fp-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }
}
