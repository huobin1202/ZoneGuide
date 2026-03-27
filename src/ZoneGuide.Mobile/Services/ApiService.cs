using ZoneGuide.Shared.Models;
using System.Net.Http.Json;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service gọi API backend
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    
    // ⚠️ Thay đổi IP tùy theo môi trường:
    // Emulator Android: 10.0.2.2
    // Thiết bị thật cùng WiFi: dùng IP LAN (ví dụ: 192.168.1.3)
    // Production: dùng domain thật (ví dụ: https://api.ZoneGuide.com)
    
    // 👇 ĐỔI IP NÀY THÀNH IP MÁY TÍNH CỦA BẠN
    private const string ServerIP = "192.168.1.3";
    private const string ServerPort = "56042"; // HTTP port (không cần SSL cho development)
    
#if ANDROID
    private static readonly string BaseUrl = $"http://{ServerIP}:{ServerPort}/api/";
#else
    private const string BaseUrl = "https://localhost:56040/api/";
#endif

    public static string NormalizeMediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            return trimmed;

#if ANDROID
        var serverRoot = $"http://{ServerIP}:{ServerPort}";
#else
        const string serverRoot = "https://localhost:56040";
#endif

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
            return $"{serverRoot}{trimmed}";

        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("images/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
        {
            return $"{serverRoot}/{trimmed}";
        }

        return trimmed;
    }

    public ApiService()
    {
        var handler = new HttpClientHandler
        {
            // Cho development, bỏ qua SSL certificate validation
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    #region POI
    
    public async Task<List<POIDto>?> GetPOIsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<POIDto>>>("pois");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<POIDto?> GetPOIAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<POIDto>>($"pois/{id}");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Tour

    public async Task<List<TourDto>?> GetToursAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<List<TourDto>>>("tours");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TourDto?> GetTourAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<TourDto>>($"tours/{id}");
            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Sync

    public async Task<SyncDataDto?> SyncDataAsync(SyncRequest request)
    {
        try
        {
            // API trả về SyncResponseDto trực tiếp (không wrap ApiResponse)
            // Gửi request tương thích với SyncRequestDto trên server
            var serverRequest = new
            {
                LastSyncTime = request.LastSyncTime,
                IncludePOIs = true,
                IncludeTours = true
            };

            var response = await _httpClient.PostAsJsonAsync("sync", serverRequest);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[ApiService] Sync response: {json[..Math.Min(json.Length, 500)]}");

                var syncResponse = System.Text.Json.JsonSerializer.Deserialize<SyncResponseFromServer>(json, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (syncResponse != null)
                {
                    return new SyncDataDto
                    {
                        LastSyncTime = syncResponse.SyncedAt,
                        POIs = syncResponse.POIs ?? new(),
                        Tours = syncResponse.Tours ?? new(),
                        DeletedPOIIds = syncResponse.DeletedPOIIds?
                            .Where(id => int.TryParse(id, out _))
                            .Select(id => int.Parse(id)).ToList() ?? new(),
                        DeletedTourIds = syncResponse.DeletedTourIds?
                            .Where(id => int.TryParse(id, out _))
                            .Select(id => int.Parse(id)).ToList() ?? new()
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] SyncDataAsync error: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// DTO tạm để deserialize response từ server (khớp SyncResponseDto phía API)
    /// </summary>
    private class SyncResponseFromServer
    {
        public bool Success { get; set; }
        public bool HasUpdates { get; set; }
        public DateTime SyncedAt { get; set; }
        public List<POIDto> POIs { get; set; } = new();
        public List<TourDto> Tours { get; set; } = new();
        public List<string> DeletedPOIIds { get; set; } = new();
        public List<string> DeletedTourIds { get; set; } = new();
    }

    #endregion

    #region Analytics

    public async Task<bool> UploadAnalyticsAsync(AnalyticsUploadDto data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("analytics/upload", data);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Download

    public async Task<byte[]?> DownloadAudioAsync(string url)
    {
        try
        {
            return await _httpClient.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        try
        {
            return await _httpClient.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
