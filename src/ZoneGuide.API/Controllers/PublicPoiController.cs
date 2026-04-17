using Microsoft.AspNetCore.Mvc;

namespace ZoneGuide.API.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class PublicPoiController : Controller
{
    [HttpGet("/poi/{id:int}")]
    public IActionResult ShowPoi(int id, [FromQuery] bool autoplay = true)
    {
        return LocalRedirect($"/webapp/index.html?poiId={id}&autoplay={autoplay.ToString().ToLowerInvariant()}");
    }
}
