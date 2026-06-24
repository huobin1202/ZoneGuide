using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ZoneGuide.API.Data;

namespace ZoneGuide.API.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class PublicWebController : Controller
{
    private readonly AppDbContext _db;

    public PublicWebController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/home")]
    public async Task<IActionResult> Home()
    {
        var pois = await _db.POIs.Where(p => p.IsActive).OrderByDescending(p => p.Priority).Take(10).ToListAsync();
        var tours = await _db.Tours.Where(t => t.IsActive).Take(10).ToListAsync();

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("ZoneGuide - Trang chủ"));

        // Hero
        sb.Append("""
        <div class="hero-section">
            <div class="hero-title">ZoneGuide</div>
            <div class="hero-subtitle">Khám phá ẩm thực địa phương</div>
        </div>
        <div class="content">
        """);

        // Featured POIs
        sb.Append("<div class=\"section\"><div class=\"section-title\">Địa điểm nổi bật</div><div class=\"h-scroll\">");
        foreach (var p in pois)
        {
            var img = !string.IsNullOrEmpty(p.ImageUrl) ? $"style=\"background-image:url('{WebUtility.HtmlEncode(p.ImageUrl)}')\"" : "class=\"card-img-placeholder\"";
            sb.Append($"<a href=\"/poi/{p.Id}\" class=\"h-card\"><div class=\"card-img-sm\" {img}></div><div class=\"card-body-sm\"><div class=\"card-name\">{WebUtility.HtmlEncode(p.Name)}</div><div class=\"card-cat\">{WebUtility.HtmlEncode(p.Category ?? "")}</div></div></a>");
        }
        sb.Append("</div></div>");

        // Tours
        sb.Append("<div class=\"section\" style=\"margin-top:20px\"><div class=\"section-title\">Tour du lịch</div><div class=\"h-scroll\">");
        foreach (var t in tours)
        {
            var img = !string.IsNullOrEmpty(t.ImageUrl) ? $"style=\"background-image:url('{WebUtility.HtmlEncode(t.ImageUrl)}')\"" : "class=\"card-img-placeholder\"";
            sb.Append($"<a href=\"/tour/{t.Id}\" class=\"h-card\"><div class=\"card-img-sm\" {img}></div><div class=\"card-body-sm\"><div class=\"card-name\">{WebUtility.HtmlEncode(t.Name)}</div><div class=\"card-cat\">{t.POICount} địa điểm</div></div></a>");
        }
        sb.Append("</div></div>");

        sb.Append("</div>");
        sb.Append(HtmlBottomNav("home"));
        sb.Append(HtmlFooter());

        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/map")]
    public async Task<IActionResult> Map()
    {
        var pois = await _db.POIs.Where(p => p.IsActive).OrderBy(p => p.Priority).ToListAsync();

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Bản đồ - ZoneGuide"));

        var poiDataJson = System.Text.Json.JsonSerializer.Serialize(pois.Select(p => new {
            id = p.Id.ToString(), name = p.Name, lat = p.Latitude, lng = p.Longitude,
            category = p.Category ?? "POI", imageUrl = p.ImageUrl ?? ""
        }));

        var categories = pois.Where(p => !string.IsNullOrEmpty(p.Category)).Select(p => p.Category!).Distinct().OrderBy(c => c).ToList();
        var catsJson = System.Text.Json.JsonSerializer.Serialize(categories);

        double centerLat = pois.Any() ? pois.Average(p => p.Latitude) : 10.762622;
        double centerLng = pois.Any() ? pois.Average(p => p.Longitude) : 106.660172;

        sb.Append($$$"""
        <style>
        .map-wrap{position:relative;width:100%;height:calc(100vh - 60px);overflow:hidden}
        #public-map{width:100%;height:100%}
        .map-search{position:absolute;top:8px;left:8px;right:8px;z-index:1000}
        .map-search input{width:100%;padding:10px 16px;border:none;border-radius:24px;font-size:14px;outline:none;box-shadow:0 2px 8px rgba(0,0,0,.15);background:white;color:#333}
        .map-chips{position:absolute;top:56px;left:8px;right:8px;z-index:1000;display:flex;gap:6px;overflow-x:auto;padding:2px 0;scrollbar-width:none}
        .map-chips::-webkit-scrollbar{display:none}
        .map-chip{padding:4px 12px;border:none;border-radius:16px;font-size:12px;cursor:pointer;white-space:nowrap;flex-shrink:0;background:#e3f2fd;color:#1976D2}
        .map-chip.active{background:#1976D2;color:white}
        .map-geo{position:absolute;bottom:180px;right:12px;z-index:1000;width:40px;height:40px;border-radius:50%;background:white;border:none;box-shadow:0 2px 8px rgba(0,0,0,.15);cursor:pointer;font-size:18px;display:flex;align-items:center;justify-content:center;color:#1976D2}
        .map-sheet{position:fixed;bottom:60px;left:0;right:0;z-index:1000;background:var(--bg-card,#fff);border-radius:20px 20px 0 0;box-shadow:0 -4px 16px rgba(0,0,0,.1);max-height:45vh;transition:transform .3s ease;transform:translateY(calc(100% - 52px));overflow:hidden}
        .map-sheet.open{transform:translateY(0)}
        .map-handle{width:36px;height:4px;background:#ddd;border-radius:2px;margin:8px auto}
        .map-sheet-title{padding:0 16px 8px;font-size:15px;font-weight:600}
        .map-sheet-list{max-height:calc(45vh - 60px);overflow-y:auto;padding:0 8px 8px}
        .map-sheet-item{display:flex;align-items:center;gap:10px;padding:8px;border-radius:10px;cursor:pointer;text-decoration:none;color:inherit}
        .map-sheet-item:hover{background:#f8f9fa}
        .map-sheet-thumb{width:40px;height:40px;border-radius:8px;overflow:hidden;flex-shrink:0;background:#e3f2fd;display:flex;align-items:center;justify-content:center;font-size:16px}
        .map-sheet-thumb img{width:100%;height:100%;object-fit:cover}
        .map-sheet-info{flex:1;overflow:hidden}
        .map-sheet-name{font-weight:600;font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .map-sheet-cat{font-size:11px;color:#999}
        .empty-map{text-align:center;padding:30px 0;color:#999;font-size:13px}
        .lds-ring{display:inline-block;position:relative;width:20px;height:20px}
        .lds-ring div{box-sizing:border-box;display:block;position:absolute;width:16px;height:16px;margin:2px;border:2px solid #1976D2;border-radius:50%;animation:lds-ring 1.2s cubic-bezier(0.5,0,0.5,1) infinite;border-color:#1976D2 transparent transparent transparent}
        .lds-ring div:nth-child(1){animation-delay:-0.45s}
        .lds-ring div:nth-child(2){animation-delay:-0.3s}
        .lds-ring div:nth-child(3){animation-delay:-0.15s}
        @@keyframes lds-ring{0%{transform:rotate(0deg)}100%{transform:rotate(360deg)}}
        @@media(prefers-color-scheme:dark){.map-search input{background:#1e1e1e;color:#e0e0e0;box-shadow:0 2px 8px rgba(0,0,0,.3)}.map-geo{background:#1e1e1e;color:#90caf9}.map-sheet{background:#1e1e1e}.map-sheet-item:hover{background:#2a2a2a}.map-handle{background:#444}.map-chip{background:#1e3a5f;color:#90caf9}.map-chip.active{background:#90caf9;color:#121212}#public-map{filter:invert(0.9) hue-rotate(180deg)}.lds-ring div{border-color:#90caf9 transparent transparent transparent}}
        </style>
        <div class="map-wrap">
            <div id="public-map"></div>
            <div class="map-search"><input id="mapSearch" type="text" placeholder="Tìm địa điểm..." oninput="filterMap()" /></div>
            <div class="map-chips" id="mapChips"></div>
            <button class="map-geo" onclick="centerOnUser()">⌖</button>
            <div class="map-sheet" id="mapSheet">
                <div class="map-handle"></div>
                <div class="map-sheet-title">Địa điểm <span id="poiCount" style="font-weight:400;font-size:12px;color:#999"></span></div>
                <div class="map-sheet-list" id="mapSheetList"><div style="text-align:center;padding:30px 0"><div class="lds-ring"><div></div><div></div><div></div><div></div></div></div></div>
            </div>
        </div>
        <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
        <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
        <script>
        var poiData = {{{poiDataJson}}};
        var categories = {{{catsJson}}};
        var map = L.map('public-map').setView([{{{centerLat}}}, {{{centerLng}}}], 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {maxZoom:19,attribution:'&copy; OpenStreetMap'}).addTo(map);
        var markers = [];
        var activeCategory = '';

        poiData.forEach(function(p) {
            var m = L.marker([p.lat, p.lng]).addTo(map);
            m.bindPopup('<a href="/poi/'+p.id+'" style="text-decoration:none;font-weight:600;color:#1976D2">'+p.name+'</a>');
            m._poiData = p;
            markers.push(m);
        });
        if (poiData.length > 1) map.fitBounds(poiData.map(function(p){return [p.lat,p.lng]}), {padding:[40,40]});

        var chipsHtml = '<button class="map-chip active" data-cat="" onclick="filterByCategory(\'\')">Tất cả</button>';
        categories.forEach(function(c) { chipsHtml += '<button class="map-chip" data-cat="'+c+'" onclick="filterByCategory(\''+c+'\')">'+c+'</button>'; });
        document.getElementById('mapChips').innerHTML = chipsHtml;

        filterMap();

        function filterByCategory(cat) {
            activeCategory = cat;
            document.querySelectorAll('.map-chip').forEach(function(c) { c.classList.toggle('active', c.dataset.cat === cat); });
            filterMap();
        }

        function filterMap() {
            var q = document.getElementById('mapSearch').value.toLowerCase().trim();
            var visible = [];
            markers.forEach(function(m) {
                var d = m._poiData;
                var match = (!activeCategory || d.category === activeCategory) && (!q || d.name.toLowerCase().includes(q));
                if (match) { m.addTo(map); visible.push(d); }
                else map.removeLayer(m);
            });
            renderSheet(visible);
            if (visible.length > 1) map.fitBounds(visible.map(function(p){return [p.lat,p.lng]}), {padding:[40,40]});
            else if (visible.length === 1) map.setView([visible[0].lat, visible[0].lng], 15);
        }

        function renderSheet(items) {
            var el = document.getElementById('mapSheetList');
            document.getElementById('poiCount').textContent = items.length ? '('+items.length+' địa điểm)' : '';
            if (!items.length) { el.innerHTML = '<div class="empty-map">Không có địa điểm</div>'; return; }
            var html = '';
            items.forEach(function(p) {
                var thumb = p.imageUrl ? '<img src="'+p.imageUrl+'" alt="'+p.name+'" />' : '<span>📍</span>';
                html += '<a href="/poi/'+p.id+'" class="map-sheet-item">'
                    + '<div class="map-sheet-thumb">'+thumb+'</div>'
                    + '<div class="map-sheet-info">'
                    + '<div class="map-sheet-name">'+p.name+'</div>'
                    + '<div class="map-sheet-cat">'+p.category+'</div></div></a>';
            });
            el.innerHTML = html;
        }

        function centerOnUser() {
            if (navigator.geolocation) navigator.geolocation.getCurrentPosition(function(pos) {
                map.setView([pos.coords.latitude, pos.coords.longitude], 15);
            }, function() {});
        }

        map.on('click', function() {
            document.getElementById('mapSheet').classList.toggle('open');
        });
        </script>
        """);

        sb.Append(HtmlBottomNav("map"));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/poi-list")]
    public async Task<IActionResult> PoiList([FromQuery] string? q, [FromQuery] string? cat)
    {
        var query = _db.POIs.Where(p => p.IsActive);
        if (!string.IsNullOrEmpty(cat)) query = query.Where(p => p.Category == cat);
        var pois = await query.OrderBy(p => p.Name).ToListAsync();

        if (!string.IsNullOrEmpty(q))
        {
            var lower = q.ToLowerInvariant();
            pois = pois.Where(p => (p.Name?.ToLowerInvariant() ?? "").Contains(lower) || (p.Address?.ToLowerInvariant() ?? "").Contains(lower)).ToList();
        }

        var categories = await _db.POIs.Where(p => p.IsActive && p.Category != null).Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Địa điểm - ZoneGuide"));

        sb.Append("<div class=\"content\">");

        // Search
        var qsafe = WebUtility.HtmlEncode(q ?? "");
        sb.Append($$"""
        <div class="search-bar">
            <form action="/poi-list" method="get">
                <input type="text" name="q" value="{{qsafe}}" placeholder="Tìm địa điểm..." class="search-input" />
                <button type="submit" class="search-btn">🔍</button>
            </form>
        </div>
        """);

        // Category chips
        sb.Append("<div class=\"cat-chips\">");
        sb.Append($"<a href=\"/poi-list\" class=\"chip {(string.IsNullOrEmpty(cat) ? "chip-active" : "")}\">Tất cả</a>");
        foreach (var c in categories)
        {
            var active = c == cat ? "chip-active" : "";
            sb.Append($"<a href=\"/poi-list?cat={WebUtility.HtmlEncode(c)}\" class=\"chip {active}\">{WebUtility.HtmlEncode(c)}</a>");
        }
        sb.Append("</div>");

        // POI list
        if (pois.Count == 0)
        {
            sb.Append("<div class=\"empty-state\">Không tìm thấy địa điểm</div>");
        }
        else
        {
            sb.Append("<div class=\"poi-list\">");
            foreach (var p in pois)
            {
                var img = !string.IsNullOrEmpty(p.ImageUrl)
                    ? $"<img src=\"{WebUtility.HtmlEncode(p.ImageUrl)}\" alt=\"{WebUtility.HtmlEncode(p.Name)}\" />"
                    : "<div class=\"thumb-icon\">📍</div>";
                sb.Append($$"""
                <a href="/poi/{{p.Id}}" class="poi-item">
                    <div class="poi-thumb">{{img}}</div>
                    <div class="poi-info">
                        <div class="poi-name">{{WebUtility.HtmlEncode(p.Name)}}</div>
                        <div class="poi-meta">{{WebUtility.HtmlEncode(p.Category ?? "")}} · ~{{p.TriggerRadius}}m</div>
                        <div class="poi-addr">{{WebUtility.HtmlEncode(p.Address ?? "")}}</div>
                    </div>
                    <div class="poi-arrow">›</div>
                </a>
                """);
            }
            sb.Append("</div>");
        }

        sb.Append("</div>");
        sb.Append(HtmlBottomNav("map"));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/tour-list")]
    public async Task<IActionResult> TourList()
    {
        var tours = await _db.Tours.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Tour du lịch - ZoneGuide"));
        sb.Append("<div class=\"content\">");

        if (tours.Count == 0)
        {
            sb.Append("<div class=\"empty-state\">Chưa có tour nào</div>");
        }
        else
        {
            foreach (var t in tours)
            {
                var img = !string.IsNullOrEmpty(t.ImageUrl)
                    ? $"style=\"background-image:url('{WebUtility.HtmlEncode(t.ImageUrl)}')\""
                    : "class=\"tour-img-placeholder\"";
                sb.Append($$"""
                <a href="/tour/{{t.Id}}" class="tour-card-lg">
                    <div class="tour-card-img" {{img}}></div>
                    <div class="tour-card-body">
                        <div class="tour-card-name">{{WebUtility.HtmlEncode(t.Name)}}</div>
                        <div class="tour-card-meta">{{t.POICount}} điểm · {{t.EstimatedDurationMinutes}} phút</div>
                        <div class="tour-card-desc">{{WebUtility.HtmlEncode(t.Description ?? "")}}</div>
                    </div>
                </a>
                """);
            }
        }

        sb.Append("</div>");
        sb.Append(HtmlBottomNav("tour-list"));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/tour/{id:int}")]
    public async Task<IActionResult> TourDetail(int id)
    {
        var tour = await _db.Tours.Include(t => t.POIs).FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
        if (tour is null)
            return Content(ErrorPage("Không tìm thấy tour"), "text/html");

        var sb = new StringBuilder();
        sb.Append(HtmlHeader($"{WebUtility.HtmlEncode(tour.Name)} - ZoneGuide"));

        var imgStyle = !string.IsNullOrEmpty(tour.ImageUrl)
            ? $"background-image:url('{WebUtility.HtmlEncode(tour.ImageUrl)}')"
            : "background:linear-gradient(135deg,#1976D2,#1565C0)";

        sb.Append($$$"""
        <style>
        .start-tour-btn{display:flex;align-items:center;justify-content:center;gap:8px;width:100%;padding:14px;background:#1976D2;color:#fff;border-radius:14px;font-size:16px;font-weight:600;text-decoration:none;cursor:pointer;border:none}
        .start-tour-btn:active{background:#1565C0}
        @@media(prefers-color-scheme:dark){.start-tour-btn{background:#1565C0;color:#fff}}
        </style>
        <div class="tour-hero" style="{{{imgStyle}}};height:200px;background-size:cover;background-position:center;position:relative;">
            <div class="tour-hero-overlay">
                <div class="tour-hero-name">{{{WebUtility.HtmlEncode(tour.Name)}}}</div>
                <div class="tour-hero-meta">{{{tour.POICount}}} địa điểm · {{{tour.EstimatedDurationMinutes}}} phút{{{(tour.DistanceKm > 0 ? " · " + tour.DistanceKm.ToString("F1") + " km" : "")}}}</div>
            </div>
        </div>
        <div class="content">
        """);

        if (!string.IsNullOrEmpty(tour.Description))
        {
            sb.Append($"<div class=\"section\"><div class=\"section-title\">Giới thiệu</div><p class=\"tour-desc\">{WebUtility.HtmlEncode(tour.Description)}</p></div>");
        }

        // Tour POI list
        var pois = tour.POIs.Where(p => p.IsActive).OrderBy(p => p.OrderInTour).ToList();
        var firstPoiId = pois.FirstOrDefault()?.Id;

        if (firstPoiId.HasValue)
        {
            sb.Append($"<div class=\"section\"><a href=\"/poi/{firstPoiId}\" class=\"start-tour-btn\"><span>▶</span> Bắt đầu tour</a></div>");
        }

        // Mini map
        var hasCoords = pois.Any(p => p.Latitude != 0 || p.Longitude != 0);
        if (hasCoords)
        {
            sb.Append("""
            <div class="section">
                <div class="section-title">Lộ trình</div>
                <div id="tour-map" style="width:100%;height:180px;border-radius:12px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,.08)"></div>
            </div>
            """);
        }

        if (pois.Count > 0)
        {
            sb.Append("<div class=\"section\"><div class=\"section-title\">Địa điểm trong tour</div><div class=\"tour-poi-list\">");
            for (int i = 0; i < pois.Count; i++)
            {
                var p = pois[i];
                var thumb = !string.IsNullOrEmpty(p.ImageUrl)
                    ? $"<img src=\"{WebUtility.HtmlEncode(p.ImageUrl)}\" alt=\"{WebUtility.HtmlEncode(p.Name)}\" />"
                    : "<div class=\"thumb-icon\">📍</div>";
                sb.Append($$"""
                <a href="/poi/{{p.Id}}" class="tour-poi-item">
                    <div class="tour-poi-order">{{i+1}}</div>
                    <div class="tour-poi-thumb">{{thumb}}</div>
                    <div class="tour-poi-name">{{WebUtility.HtmlEncode(p.Name)}}</div>
                    <div class="poi-arrow">›</div>
                </a>
                """);
            }
            sb.Append("</div></div>");
        }

        sb.Append("</div>");

        // Leaflet + map JS
        if (hasCoords)
        {
            var pointsJson = JsonSerializer.Serialize(pois.Where(p => p.Latitude != 0 || p.Longitude != 0).Select(p => new { lat = p.Latitude, lng = p.Longitude, name = p.Name }));
            sb.Append($$"""
            <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
            <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
            <script>
            (function() {
                var points = {{pointsJson}};
                if (!points.length) return;
                var map = L.map('tour-map', { zoomControl: false, dragging: false, scrollWheelZoom: false });
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '' }).addTo(map);
                var latlngs = points.map(function(p) { return [p.lat, p.lng]; });
                L.polyline(latlngs, { color: '#1976D2', weight: 3 }).addTo(map);
                points.forEach(function(p) { L.marker([p.lat, p.lng]).addTo(map); });
                map.fitBounds(latlngs, { padding: [30, 30] });
            })();
            </script>
            """);
        }

        sb.Append(HtmlBottomNav("tour-list"));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/more")]
    public IActionResult More()
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Thêm - ZoneGuide"));
        sb.Append("""
        <div class="content">
            <div class="menu-section">
                <div class="menu-section-title">Khám phá</div>
                <div class="menu">
                    <a href="/map" class="menu-item"><span class="menu-icon">🗺️</span><span>Bản đồ</span><span class="menu-arrow">›</span></a>
                    <a href="/poi-list" class="menu-item"><span class="menu-icon">📍</span><span>Danh sách địa điểm</span><span class="menu-arrow">›</span></a>
                    <a href="/tour-list" class="menu-item"><span class="menu-icon">🗺️</span><span>Tour du lịch</span><span class="menu-arrow">›</span></a>
                </div>
            </div>
            <div class="menu-section">
                <div class="menu-section-title">Hoạt động</div>
                <div class="menu">
                    <a href="/history" class="menu-item"><span class="menu-icon">◷</span><span>Lịch sử</span><span class="menu-arrow">›</span></a>
                    <a href="/settings" class="menu-item"><span class="menu-icon">⚙</span><span>Cài đặt</span><span class="menu-arrow">›</span></a>
                </div>
            </div>
            <div class="menu-section">
                <div class="menu-section-title">Thông tin</div>
                <div class="menu">
                    <div class="menu-item"><span class="menu-icon">ℹ️</span><span>Phiên bản 1.0.0</span></div>
                </div>
            </div>
        </div>
        """);
        sb.Append(HtmlBottomNav("more"));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/history")]
    public IActionResult History()
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Lịch sử - ZoneGuide"));
        sb.Append("""
        <style>
        .filter-chips { display: flex; gap: 6px; overflow-x: auto; padding-bottom: 12px; scrollbar-width: none; margin-bottom:4px }
        .filter-chips::-webkit-scrollbar { display: none; }
        .filter-chip { display: flex; align-items: center; gap: 4px; padding: 6px 14px; background: #e3f2fd; color: #1976D2; border: none; border-radius: 18px; font-size: 13px; cursor: pointer; white-space: nowrap; flex-shrink: 0; }
        .filter-chip.active { background: #1976D2; color: white; }
        .empty-state { text-align: center; padding: 60px 20px; }
        .empty-icon { font-size: 48px; color: #ccc; margin-bottom: 12px; }
        .empty-title { font-size: 18px; font-weight: 600; color: #666; margin-bottom: 6px; }
        .empty-subtitle { font-size: 13px; color: #999; }
        .day-group { margin-bottom: 16px; }
        .day-label { font-size: 16px; font-weight: 600; color: #444; padding: 0 4px 8px; }
        .history-item { display: flex; align-items: center; gap: 12px; background: white; border-radius: 12px; padding: 10px; margin-bottom: 8px; box-shadow: 0 1px 4px rgba(0,0,0,.06); }
        .history-thumb { width: 56px; height: 56px; border-radius: 10px; flex-shrink: 0; display: flex; align-items: center; justify-content: center; background: #e3f2fd; overflow: hidden; }
        .thumb-placeholder { font-size: 22px; display:flex; align-items:center; justify-content:center; }
        .history-info { flex: 1; overflow: hidden; }
        .history-name { font-weight: 600; font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .history-meta { font-size: 11px; color: #999; margin-top: 2px; display: flex; gap: 4px; align-items: center; flex-wrap: wrap; }
        .history-category { background: #e3f2fd; color: #1976D2; padding: 0 6px; border-radius: 8px; font-size: 10px; }
        .history-actions { display: flex; flex-direction: column; gap: 4px; flex-shrink: 0; }
        .history-play-btn, .history-del-btn { width: 36px; height: 36px; border-radius: 50%; border: none; cursor: pointer; font-size: 14px; padding: 0; display: flex; align-items: center; justify-content: center; }
        .history-play-btn { background: #1976D2; color: white; }
        .history-del-btn { background: #ffebee; color: #e53935; font-size: 13px; }
        @@media(prefers-color-scheme:dark){.filter-chip{background:#1e3a5f;color:#90caf9}.filter-chip.active{background:#90caf9;color:#121212}.history-item{background:#1e1e1e}.history-name{color:#e0e0e0}.day-label{color:#aaa}.history-category{background:#1e3a5f;color:#90caf9}.history-del-btn{background:#3e1e1e;color:#ef5350}}
        </style>
        <div class="content">
            <div class="filter-chips" id="filter-chips">
                <button class="filter-chip active" data-filter="" onclick="filterHistory('')">◷ Tổng quan</button>
                <button class="filter-chip" data-filter="today" onclick="filterHistory('today')">Hôm nay</button>
                <button class="filter-chip" data-filter="yesterday" onclick="filterHistory('yesterday')">Hôm qua</button>
                <button class="filter-chip" data-filter="week" onclick="filterHistory('week')">Tuần này</button>
                <button class="filter-chip" data-filter="month" onclick="filterHistory('month')">Tháng này</button>
                <button class="filter-chip" data-filter="earlier" onclick="filterHistory('earlier')">Cũ hơn</button>
            </div>
            <div id="history-list"></div>
        </div>
        <script>
        function renderHistory(filter) {
            var list = document.getElementById('history-list');
            try { var items = JSON.parse(localStorage.getItem('zg_history')) || []; } catch(e) { items = []; }
            var now = new Date(), today = new Date(now.getFullYear(),now.getMonth(),now.getDate());
            var yesterday = new Date(today); yesterday.setDate(yesterday.getDate()-1);
            var weekStart = new Date(today); weekStart.setDate(weekStart.getDate()-weekStart.getDay());
            var monthStart = new Date(now.getFullYear(), now.getMonth(), 1);

            items = items.filter(function(i) {
                if (!filter) return true;
                var d = new Date(i.timestamp);
                if (filter === 'today') return d >= today;
                if (filter === 'yesterday') return d >= yesterday && d < today;
                if (filter === 'week') return d >= weekStart && d < yesterday;
                if (filter === 'month') return d >= monthStart && d < weekStart;
                if (filter === 'earlier') return d < monthStart;
                return true;
            });

            if (!items.length) {
                list.innerHTML = '<div class="empty-state"><div class="empty-icon">&#9733;</div><div class="empty-title">Chưa có lịch sử</div><div class="empty-subtitle">Các địa điểm bạn đã nghe sẽ xuất hiện ở đây</div></div>';
                return;
            }

            var groups = {};
            items.forEach(function(i) {
                var d = new Date(i.timestamp);
                var key, label;
                if (d >= today) { key = 'today'; label = 'Hôm nay'; }
                else if (d >= yesterday) { key = 'yesterday'; label = 'Hôm qua'; }
                else { key = d.toISOString().slice(0,10); label = d.getDate()+'/'+(d.getMonth()+1)+'/'+d.getFullYear(); }
                if (!groups[key]) groups[key] = { label: label, items: [] };
                groups[key].items.push(i);
            });

            var html = '';
            var keys = Object.keys(groups).sort(function(a,b) {
                if (a === 'today') return -1; if (b === 'today') return 1;
                if (a === 'yesterday') return -1; if (b === 'yesterday') return 1;
                return b.localeCompare(a);
            });
            keys.forEach(function(key) {
                html += '<div class="day-group"><div class="day-label">'+groups[key].label+'</div>';
                groups[key].items.forEach(function(item) {
                    var imgStyle = item.imageUrl ? "background-image:url('"+item.imageUrl+"')" : "";
                    var thumb = item.imageUrl ? '<div class="history-thumb" style="'+imgStyle+';background-size:cover;background-position:center"></div>'
                        : '<div class="history-thumb thumb-placeholder">&#128205;</div>';
                    var cat = item.category ? '<span class="history-category">'+item.category+'</span><span>&#8226;</span>' : '';
                    html += '<div class="history-item">'
                        + thumb
                        + '<div class="history-info">'
                        + '<div class="history-name">'+item.name+'</div>'
                        + '<div class="history-meta">'+cat+' <span>'+formatDuration(item.durationSeconds)+'</span><span>&#8226;</span><span>'+timeAgo(item.timestamp)+'</span></div>'
                        + '<div class="history-meta"><span>'+(item.language||'vi')+'</span><span>&#8226;</span><span>Đã nghe '+(item.playCount||1)+' lần</span></div>'
                        + '</div>'
                        + '<div class="history-actions">'
                        + '<button class="history-play-btn" onclick="location.href=\'/poi/'+item.id+'\'">&#9654;</button>'
                        + '<button class="history-del-btn" onclick="deleteHistoryEntry('+item.id+',\''+item.timestamp+'\')">&#128465;</button>'
                        + '</div></div>';
                });
                html += '</div>';
            });
            list.innerHTML = html;
        }
        function formatDuration(s) { if (!s||s<=0) return '< 1 phút'; var m=Math.floor(s/60); return m<60 ? m+' phút' : Math.floor(m/60)+'h '+(m%60)+'m'; }
        function timeAgo(ts) { var d=new Date(ts),n=new Date(),diff=Math.floor((n-d)/60000); if(diff<1) return 'Vừa xong'; if(diff<60) return diff+' phút trước'; if(diff<1440) return Math.floor(diff/60)+' giờ trước'; return d.getHours().toString().padStart(2,'0')+':'+d.getMinutes().toString().padStart(2,'0'); }
        function filterHistory(f) { document.querySelectorAll('.filter-chip').forEach(function(c){c.classList.toggle('active',c.dataset.filter===f)}); renderHistory(f); }
        function deleteHistoryEntry(id,ts) { try{var h=JSON.parse(localStorage.getItem('zg_history')||'[]');h=h.filter(function(e){return!(e.id===id&&e.timestamp===ts)});localStorage.setItem('zg_history',JSON.stringify(h))}catch(e){} renderHistory(document.querySelector('.filter-chip.active')?.dataset?.filter||''); }
        renderHistory('');
        </script>
        """);
        sb.Append(HtmlBottomNav(""));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/settings")]
    public IActionResult Settings()
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHeader("Cài đặt - ZoneGuide"));
        sb.Append("""
        <div class="content">
            <div class="settings-section">
                <div class="settings-section-title">Âm thanh</div>
                <div class="settings-card">
                    <div class="setting-row">
                        <div class="setting-label">
                            <div class="setting-name">Tự động phát</div>
                            <div class="setting-desc">Tự động phát thuyết minh khi mở địa điểm</div>
                        </div>
                        <label class="switch"><input type="checkbox" id="autoPlay" onchange="saveSetting('autoPlay',this.checked)"><span class="switch-slider"></span></label>
                    </div>
                    <div class="setting-row">
                        <div class="setting-label">
                            <div class="setting-name">Ngôn ngữ mặc định</div>
                            <div class="setting-desc">Ngôn ngữ ưu tiên khi phát thuyết minh</div>
                        </div>
                        <select class="setting-select" id="language" onchange="saveSetting('language',this.value)">
                            <option value="vi">Tiếng Việt</option>
                            <option value="en">English</option>
                            <option value="ja">日本語</option>
                            <option value="ko">한국어</option>
                            <option value="zh">中文</option>
                        </select>
                    </div>
                    <div class="setting-row">
                        <div class="setting-label">
                            <div class="setting-name">Tốc độ phát</div>
                            <div class="setting-desc">Tốc độ phát âm thanh (chỉ hỗ trợ TTS)</div>
                        </div>
                        <div class="setting-slider-group">
                            <input type="range" class="setting-slider" min="0.5" max="2.0" step="0.1" id="ttsSpeed" oninput="document.getElementById('speedVal').textContent=parseFloat(this.value).toFixed(1)+'x'" onchange="saveSetting('ttsSpeed',parseFloat(this.value))">
                            <span class="setting-slider-value" id="speedVal">1.0x</span>
                        </div>
                    </div>
                </div>
            </div>
            <div class="settings-section">
                <div class="settings-section-title">Dữ liệu</div>
                <div class="settings-card">
                    <div class="setting-row" style="border-bottom:none">
                        <div class="setting-label">
                            <div class="setting-name">Xóa lịch sử</div>
                            <div class="setting-desc">Xóa toàn bộ lịch sử nghe trên trình duyệt</div>
                        </div>
                        <button class="setting-btn setting-btn-danger" onclick="if(confirm('Xóa toàn bộ lịch sử nghe?')){localStorage.removeItem('zg_history');alert('Đã xóa lịch sử')}">Xóa</button>
                    </div>
                </div>
            </div>
            <div class="settings-section">
                <div class="settings-section-title">Thông tin</div>
                <div class="settings-card">
                    <div class="setting-row" style="border-bottom:none">
                        <div class="setting-label"><div class="setting-name">Phiên bản</div></div>
                        <span class="setting-value">1.0.0</span>
                    </div>
                </div>
            </div>
        </div>
        <script>
        function loadSettings() {
            try { var s = JSON.parse(localStorage.getItem('zg_settings')) || {}; } catch(e) { var s = {}; }
            if (document.getElementById('autoPlay')) document.getElementById('autoPlay').checked = s.autoPlay !== false;
            if (document.getElementById('language')) document.getElementById('language').value = s.language || 'vi';
            if (document.getElementById('ttsSpeed')) { var v = s.ttsSpeed || 1.0; document.getElementById('ttsSpeed').value = v; document.getElementById('speedVal').textContent = parseFloat(v).toFixed(1)+'x'; }
        }
        function saveSetting(key, val) {
            try { var s = JSON.parse(localStorage.getItem('zg_settings')) || {}; } catch(e) { var s = {}; }
            s[key] = val; localStorage.setItem('zg_settings', JSON.stringify(s));
        }
        loadSettings();
        </script>
        <style>
        .settings-section { margin-bottom: 24px; }
        .settings-section-title { font-size: 15px; font-weight: 600; color: #444; margin-bottom: 8px; padding: 0 4px; }
        .settings-card { background: white; border-radius: 14px; overflow: hidden; box-shadow: 0 1px 4px rgba(0,0,0,.06); }
        .setting-row { display: flex; align-items: center; justify-content: space-between; padding: 14px 16px; border-bottom: 1px solid #f0f0f0; gap: 12px; }
        .setting-label { flex: 1; overflow: hidden; }
        .setting-name { font-size: 14px; font-weight: 500; }
        .setting-desc { font-size: 12px; color: #999; margin-top: 2px; }
        .switch { position: relative; display: inline-block; width: 44px; height: 24px; flex-shrink: 0; }
        .switch input { opacity: 0; width: 0; height: 0; }
        .switch-slider { position: absolute; cursor: pointer; inset: 0; background: #ccc; border-radius: 24px; transition: .3s; }
        .switch-slider::before { content: ""; position: absolute; height: 18px; width: 18px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: .3s; }
        .switch input:checked + .switch-slider { background: #1976D2; }
        .switch input:checked + .switch-slider::before { transform: translateX(20px); }
        .setting-select { padding: 6px 10px; border: 1px solid #e0e0e0; border-radius: 10px; font-size: 13px; background: white; outline: none; cursor: pointer; }
        .setting-slider-group { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
        .setting-slider { width: 100px; height: 4px; -webkit-appearance: none; appearance: none; border-radius: 2px; outline: none; }
        .setting-slider::-webkit-slider-thumb { -webkit-appearance: none; width: 16px; height: 16px; border-radius: 50%; background: #1976D2; cursor: pointer; }
        .setting-slider-value { font-size: 13px; font-weight: 600; min-width: 36px; text-align: right; }
        .setting-btn { padding: 6px 16px; border: none; border-radius: 10px; background: #e3f2fd; color: #1976D2; font-size: 13px; font-weight: 500; cursor: pointer; flex-shrink: 0; }
        .setting-btn-danger { background: #ffebee; color: #e53935; }
        .setting-value { font-size: 14px; color: #999; }
        @@media(prefers-color-scheme:dark){.settings-card{background:#1e1e1e}.settings-section-title{color:#aaa}.setting-name{color:#e0e0e0}.setting-desc{color:#777}.setting-select{background:#1e1e1e;color:#e0e0e0;border-color:#333}.setting-btn{background:#1e3a5f;color:#90caf9}.setting-btn-danger{background:#3e1e1e;color:#ef5350}}
        </style>
        """);
        sb.Append(HtmlBottomNav(""));
        sb.Append(HtmlFooter());
        return Content(sb.ToString(), "text/html", Encoding.UTF8);
    }

    [HttpGet("/")]
    public IActionResult Root()
    {
        return Redirect("/home");
    }

    // --- Shared HTML helpers ---

    private static string HtmlHeader(string title)
    {
        return $$$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no"/>
        <meta name="theme-color" content="#1976D2"/>
        <title>{{{WebUtility.HtmlEncode(title)}}}</title>
        <style>
        *{margin:0;padding:0;box-sizing:border-box}
        body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f6f7fb;color:#333;padding-bottom:80px}
        a{text-decoration:none;color:inherit}
        .hero-section{background:linear-gradient(135deg,#1976D2,#1565C0);color:white;padding:24px 20px 32px;border-radius:0 0 28px 28px;margin-bottom:16px}
        .hero-title{font-size:24px;font-weight:700;margin-bottom:4px}
        .hero-subtitle{font-size:14px;opacity:.85}
        .content{padding:0 16px}
        .section-title{font-size:15px;font-weight:600;margin-bottom:10px;color:#444}
        .h-scroll{display:flex;gap:12px;overflow-x:auto;padding-bottom:8px;-webkit-overflow-scrolling:touch;scrollbar-width:none}
        .h-scroll::-webkit-scrollbar{display:none}
        .h-card{flex:0 0 150px;background:white;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)}
        .card-img-sm{width:150px;height:90px;background-size:cover;background-position:center}
        .card-img-placeholder{background:#e3f2fd;display:flex;align-items:center;justify-content:center;color:#1976D2;font-size:24px}
        .card-body-sm{padding:8px 10px}
        .card-name{font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .card-cat{font-size:11px;color:#999;margin-top:2px}
        .search-bar{margin-bottom:12px}
        .search-input{width:100%;padding:10px 14px;border:1px solid #e0e0e0;border-radius:24px;font-size:14px;outline:none;background:white}
        .search-input:focus{border-color:#1976D2}
        .search-btn{display:none}
        .cat-chips{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:16px}
        .chip{padding:4px 12px;background:#e3f2fd;color:#1976D2;border-radius:16px;font-size:12px;white-space:nowrap}
        .chip-active{background:#1976D2;color:white}
        .empty-state{text-align:center;padding:40px 0;color:#999;font-size:14px}
        .poi-list,.tour-poi-list{display:flex;flex-direction:column;gap:8px}
        .poi-item,.tour-poi-item{display:flex;align-items:center;background:white;border-radius:12px;padding:10px 12px;box-shadow:0 1px 4px rgba(0,0,0,.06);gap:10px}
        .poi-thumb,.tour-poi-thumb{width:48px;height:48px;border-radius:10px;overflow:hidden;flex-shrink:0;background:#e3f2fd;display:flex;align-items:center;justify-content:center;font-size:20px}
        .poi-thumb img,.tour-poi-thumb img{width:100%;height:100%;object-fit:cover}
        .poi-info{flex:1;overflow:hidden}
        .poi-name{font-weight:600;font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .poi-meta{font-size:11px;color:#999;margin:2px 0}
        .poi-addr{font-size:11px;color:#999;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
        .poi-arrow{font-size:20px;color:#ccc;flex-shrink:0}
        .tour-card-lg{display:block;background:white;border-radius:14px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);margin-bottom:12px}
        .tour-card-img{width:100%;height:140px;background-size:cover;background-position:center}
        .tour-img-placeholder{height:100px;background:linear-gradient(135deg,#e3f2fd,#bbdefb);display:flex;align-items:center;justify-content:center;color:#1976D2;font-size:24px}
        .tour-card-body{padding:10px 14px}
        .tour-card-name{font-weight:600;font-size:15px;margin-bottom:2px}
        .tour-card-meta{font-size:12px;color:#999;margin-bottom:4px}
        .tour-card-desc{font-size:13px;color:#666;line-height:1.4;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden}
        .tour-hero-overlay{position:absolute;bottom:0;left:0;right:0;padding:16px 20px;background:linear-gradient(transparent,rgba(0,0,0,.7));color:white}
        .tour-hero-name{font-size:22px;font-weight:700;margin-bottom:2px}
        .tour-hero-meta{font-size:13px;opacity:.85}
        .section{margin-bottom:20px}
        .tour-desc{line-height:1.6;color:#444;font-size:14px}
        .tour-poi-order{width:22px;height:22px;background:#1976D2;color:white;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:600;flex-shrink:0}
        .tour-poi-name{flex:1;font-size:13px;font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
        .menu-section{margin-bottom:20px}
        .menu-section-title{font-size:14px;font-weight:600;margin-bottom:8px;color:#444}
        .menu{background:white;border-radius:12px;overflow:hidden}
        .menu-item{display:flex;align-items:center;gap:10px;padding:12px 14px;border-bottom:1px solid #f0f0f0;font-size:14px}
        .menu-item:last-child{border-bottom:none}
        .menu-icon{font-size:18px}
        .menu-arrow{margin-left:auto;color:#ccc;font-size:18px}
        .bottom-nav{position:fixed;bottom:0;left:0;right:0;background:white;display:flex;justify-content:space-around;align-items:center;height:60px;padding-bottom:env(safe-area-inset-bottom,4px);box-shadow:0 -2px 10px rgba(0,0,0,.1);z-index:100}
        .nav-item{display:flex;flex-direction:column;align-items:center;color:#999;padding:4px 8px;font-size:10px;gap:2px}
        .nav-item.active{color:#1976D2}
        .nav-item svg{width:22px;height:22px;fill:currentColor}
        .thumb-icon{font-size:20px}
        @@media(prefers-color-scheme:dark){body{background:#121212;color:#e0e0e0}.section-title{color:#aaa}.h-card,.poi-item,.tour-card-lg,.menu,.tour-poi-item{background:#1e1e1e}.tour-desc{color:#bbb}.chip{background:#1e3a5f;color:#90caf9}.chip-active{background:#90caf9;color:#121212}.bottom-nav{background:#1e1e1e;border-color:#333}.search-input{background:#1e1e1e;border-color:#333;color:#e0e0e0}}
        </style>
        </head>
        <body>
        """;
    }

    private static string HtmlBottomNav(string active)
    {
        return $$"""
        <nav class="bottom-nav">
            <a href="/home" class="nav-item {{(active == "home" ? "active" : "")}}">
                <svg viewBox="0 0 24 24"><path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/></svg>
                <span>Trang chủ</span>
            </a>
            <a href="/map" class="nav-item {{(active == "map" ? "active" : "")}}">
                <svg viewBox="0 0 24 24"><path d="M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z"/></svg>
                <span>Bản đồ</span>
            </a>
            <a href="/tour-list" class="nav-item {{(active == "tour-list" ? "active" : "")}}">
                <svg viewBox="0 0 24 24"><path d="M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z"/></svg>
                <span>Tour</span>
            </a>
            <a href="/more" class="nav-item {{(active == "more" ? "active" : "")}}">
                <svg viewBox="0 0 24 24"><path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg>
                <span>Thêm</span>
            </a>
        </nav>
        """;
    }

    private static string HtmlFooter()
    {
        return "</body></html>";
    }

    private static string ErrorPage(string message)
    {
        return $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/><title>{WebUtility.HtmlEncode(message)}</title><style>body{{font-family:sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;color:#999;text-align:center;padding:20px}}</style></head><body><h2>{WebUtility.HtmlEncode(message)}</h2></body></html>";
    }
}
