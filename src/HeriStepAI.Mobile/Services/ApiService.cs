using HeriStepAI.Shared.Models;
using System.Net.Http.Json;

namespace HeriStepAI.Mobile.Services;

/// <summary>
/// Service gọi API backend
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    
    // ⚠️ QUAN TRỌNG: Thay đổi IP cho thiết bị thật
    // Emulator Android: 10.0.2.2
    // Thiết bị thật: Dùng IP LAN của máy tính (ví dụ: 192.168.1.100)
    // Production: Dùng domain thật (ví dụ: https://api.heristepai.com)
#if ANDROID
    private const string BaseUrl = "https://10.0.2.2:56040/api";
#else
    private const string BaseUrl = "https://localhost:56040/api";
#endif

    public ApiService()
    {
        // Cho development, bỏ qua SSL certificate validation
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15) // Giảm timeout để không block quá lâu
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
            var response = await _httpClient.PostAsJsonAsync("sync", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SyncDataDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore
        }
        return null;
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
