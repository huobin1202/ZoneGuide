using System.Net.Http.Json;
using System.Net.Http.Headers;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Admin.Services;

public interface IApiService
{
    // POI Operations
    Task<List<POIDto>> GetPOIsAsync();
    Task<POIDto?> GetPOIAsync(string id);
    Task<List<POITranslationDto>> GetPOITranslationsAsync(string poiId);
    Task<POITranslationDto?> UpsertPOITranslationAsync(string poiId, string languageCode, POITranslationDto dto);
    Task<bool> DeletePOITranslationAsync(string poiId, string languageCode);
    Task<TranslatedPOIContentDto?> TranslatePOIContentAsync(TranslatePOIContentRequestDto request);
    Task<POIDto?> CreatePOIAsync(CreatePOIDto dto);
    Task<POIDto?> UpdatePOIAsync(string id, UpdatePOIDto dto);
    Task<bool> DeletePOIAsync(string id);

    // POI QR codes
    Task<List<PoiQrCodeDto>> GetPoiQRCodesAsync(bool includeInactive = false);
    Task<PoiQrCodeDto?> GeneratePoiQrCodeAsync(string poiId, bool force = false);
    Task<int> GenerateMissingPoiQRCodesAsync(bool includeInactive = false, bool force = false);

    // Tour Operations
    Task<List<TourDto>> GetToursAsync();
    Task<TourDto?> GetTourAsync(string id);
    Task<TourWithPOIsDto?> GetTourWithDetailsAsync(string id);
    Task<List<TourTranslationDto>> GetTourTranslationsAsync(string tourId);
    Task<TourTranslationDto?> UpsertTourTranslationAsync(string tourId, string languageCode, TourTranslationDto dto);
    Task<bool> DeleteTourTranslationAsync(string tourId, string languageCode);
    Task<TourDto?> CreateTourAsync(CreateTourDto dto);
    Task<TourDto?> UpdateTourAsync(string id, UpdateTourDto dto);
    Task<bool> DeleteTourAsync(string id);

    // Analytics
    Task<DashboardAnalyticsDto?> GetDashboardAsync(DateTime? from = null, DateTime? to = null);
    Task<List<TopPOIDto>> GetTopPOIsAsync(DateTime? from = null, DateTime? to = null, int count = 10);
    Task<List<HeatmapPointDto>> GetHeatmapDataAsync(DateTime? from = null, DateTime? to = null);
    Task<QrMonitoringSnapshotDto?> GetQrMonitoringSnapshotAsync();
    Task<MobileLiveMonitoringSnapshotDto?> GetMobileMonitoringSnapshotAsync();

    // TTS
    Task<string?> GenerateTtsAsync(string text, string language);
    Task<string?> UploadAudioAsync(byte[] bytes, string fileName, string? contentType = null);

    // Notifications
    Task<List<NotificationDto>> GetNotificationsAsync(bool? isRead = null, bool isDeleted = false);
    Task<NotificationDto?> GetNotificationAsync(int id);
    Task<int> GetUnreadCountAsync();
    Task<bool> MarkAsReadAsync(int id);
    Task<bool> MarkAllAsReadAsync();
    Task<bool> DeleteNotificationAsync(int id);
    Task<bool> RestoreNotificationAsync(int id);

    // Generic
    Task<T?> GetAsync<T>(string url) where T : class;
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private PoiQrCodeDto NormalizeQrCodeDto(PoiQrCodeDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.QrUrl) && Uri.TryCreate(_httpClient.BaseAddress, dto.QrUrl, out var absoluteUri))
        {
            dto.QrUrl = absoluteUri.ToString();
        }

        return dto;
    }

    // POI Operations
        public async Task<List<POIDto>> GetPOIsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<POIDto>>("api/pois/all") ?? new();
            }
            catch
            {
                return new();
            }
        }    public async Task<POIDto?> GetPOIAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<POIDto>($"api/pois/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<POITranslationDto>> GetPOITranslationsAsync(string poiId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<POITranslationDto>>($"api/pois/{poiId}/translations") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<POITranslationDto?> UpsertPOITranslationAsync(string poiId, string languageCode, POITranslationDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/pois/{poiId}/translations/{Uri.EscapeDataString(languageCode)}", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<POITranslationDto>();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeletePOITranslationAsync(string poiId, string languageCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/pois/{poiId}/translations/{Uri.EscapeDataString(languageCode)}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TranslatedPOIContentDto?> TranslatePOIContentAsync(TranslatePOIContentRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/pois/translate-content", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TranslatedPOIContentDto>();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<POIDto?> CreatePOIAsync(CreatePOIDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/pois", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<POIDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<POIDto?> UpdatePOIAsync(string id, UpdatePOIDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/pois/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<POIDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeletePOIAsync(string id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/pois/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // POI QR codes
    public async Task<List<PoiQrCodeDto>> GetPoiQRCodesAsync(bool includeInactive = false)
    {
        try
        {
            var url = $"api/qrcodes/pois?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
            var items = await _httpClient.GetFromJsonAsync<List<PoiQrCodeDto>>(url) ?? new List<PoiQrCodeDto>();
            return items.Select(NormalizeQrCodeDto).ToList();
        }
        catch
        {
            return new List<PoiQrCodeDto>();
        }
    }

    public async Task<PoiQrCodeDto?> GeneratePoiQrCodeAsync(string poiId, bool force = false)
    {
        try
        {
            var url = $"api/qrcodes/pois/{Uri.EscapeDataString(poiId)}/generate?force={force.ToString().ToLowerInvariant()}";
            var response = await _httpClient.PostAsync(url, content: null);
            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<PoiQrCodeDto>();
            return dto == null ? null : NormalizeQrCodeDto(dto);
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> GenerateMissingPoiQRCodesAsync(bool includeInactive = false, bool force = false)
    {
        try
        {
            var url = $"api/qrcodes/pois/generate-missing?includeInactive={includeInactive.ToString().ToLowerInvariant()}&force={force.ToString().ToLowerInvariant()}";
            var response = await _httpClient.PostAsync(url, content: null);
            if (!response.IsSuccessStatusCode)
                return 0;

            var value = await response.Content.ReadFromJsonAsync<int>();
            return value;
        }
        catch
        {
            return 0;
        }
    }

    // Tour Operations
    public async Task<List<TourDto>> GetToursAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TourDto>>("api/tours/all") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<TourDto?> GetTourAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TourDto>($"api/tours/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TourWithPOIsDto?> GetTourWithDetailsAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TourWithPOIsDto>($"api/tours/{id}/details");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TourTranslationDto>> GetTourTranslationsAsync(string tourId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TourTranslationDto>>($"api/tours/{tourId}/translations") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<TourTranslationDto?> UpsertTourTranslationAsync(string tourId, string languageCode, TourTranslationDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tours/{tourId}/translations/{Uri.EscapeDataString(languageCode)}", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TourTranslationDto>();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteTourTranslationAsync(string tourId, string languageCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/tours/{tourId}/translations/{Uri.EscapeDataString(languageCode)}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TourDto?> CreateTourAsync(CreateTourDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/tours", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TourDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TourDto?> UpdateTourAsync(string id, UpdateTourDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tours/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TourDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteTourAsync(string id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/tours/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Analytics
    public async Task<DashboardAnalyticsDto?> GetDashboardAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = "";
            if (from.HasValue || to.HasValue)
            {
                var parts = new List<string>();
                if (from.HasValue) parts.Add($"from={from.Value:yyyy-MM-dd}");
                if (to.HasValue) parts.Add($"to={to.Value:yyyy-MM-dd}");
                query = "?" + string.Join("&", parts);
            }
            return await _httpClient.GetFromJsonAsync<DashboardAnalyticsDto>($"api/analytics/dashboard{query}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TopPOIDto>> GetTopPOIsAsync(DateTime? from = null, DateTime? to = null, int count = 10)
    {
        try
        {
            var queryParts = new List<string> { $"count={count}" };

            if (from.HasValue)
            {
                queryParts.Add($"from={from.Value:yyyy-MM-dd}");
            }

            if (to.HasValue)
            {
                queryParts.Add($"to={to.Value:yyyy-MM-dd}");
            }

            var query = string.Join("&", queryParts);
            return await _httpClient.GetFromJsonAsync<List<TopPOIDto>>($"api/analytics/top-pois?{query}") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<HeatmapPointDto>> GetHeatmapDataAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var query = "";
            if (from.HasValue || to.HasValue)
            {
                var parts = new List<string>();
                if (from.HasValue) parts.Add($"from={from.Value:yyyy-MM-dd}");
                if (to.HasValue) parts.Add($"to={to.Value:yyyy-MM-dd}");
                query = "?" + string.Join("&", parts);
            }
            return await _httpClient.GetFromJsonAsync<List<HeatmapPointDto>>($"api/analytics/heatmap{query}") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<QrMonitoringSnapshotDto?> GetQrMonitoringSnapshotAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<QrMonitoringSnapshotDto>("api/qr-monitoring/snapshot");
        }
        catch
        {
            return null;
        }
    }

    // Notifications
    public async Task<List<NotificationDto>> GetNotificationsAsync(bool? isRead = null, bool isDeleted = false)
    {
        try
        {
            var query = $"api/notifications?isDeleted={isDeleted.ToString().ToLowerInvariant()}";
            if (isRead.HasValue)
            {
                query += $"&isRead={isRead.Value.ToString().ToLowerInvariant()}";
            }
            return await _httpClient.GetFromJsonAsync<List<NotificationDto>>(query) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<NotificationDto?> GetNotificationAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationDto>($"api/notifications/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count");
            return result?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> MarkAsReadAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/notifications/{id}/read", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MarkAllAsReadAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/notifications/mark-all-read", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/notifications/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RestoreNotificationAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/notifications/{id}/restore", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region Dashboard monitoring QR va mobile

    public async Task<MobileLiveMonitoringSnapshotDto?> GetMobileMonitoringSnapshotAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MobileLiveMonitoringSnapshotDto>("api/mobile-monitoring/snapshot");
        }
        catch
        {
            return null;
        }
    }

    #endregion

    public async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<T>($"api/{url}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GenerateTtsAsync(string text, string language)
    {
        try
        {
            var request = new GenerateTtsRequest { Text = text, Language = language };
            var response = await _httpClient.PostAsJsonAsync("api/audio/generate-tts", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateTtsResponse>();
                if (result != null && result.Success)
                {
                    return result.AudioPath ?? result.AudioUrl;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> UploadAudioAsync(byte[] bytes, string fileName, string? contentType = null)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(bytes);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType);
            form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.mp3" : fileName);

            var response = await _httpClient.PostAsync("api/tts/upload", form);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return result.GetProperty("audioUrl").GetString();
        }
        catch
        {
            return null;
        }
    }
}

// Analytics DTOs are defined in ZoneGuide.API.Services.SyncService
// We use those definitions through the Shared project

public sealed class QrScanLogDto
{
    public DateTime ScannedAtUtc { get; set; }
    public int PoiId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int SignalStrength { get; set; } // 0 = Mạnh, 1 = Yếu
}

public sealed class QrMonitoringSnapshotDto
{
    public int ActiveDeviceCount { get; set; }
    public int UniqueDeviceCount { get; set; }
    public long TotalAccessCount { get; set; }
    public int AccessesLastMinute { get; set; }
    public DateTime? LastAccessAtUtc { get; set; }
    public int? LastPoiId { get; set; }
    public int ActiveWindowSeconds { get; set; }
    public int LastMinuteWindowSeconds { get; set; }
    public List<QrScanLogDto> RecentScans { get; set; } = new();
}
