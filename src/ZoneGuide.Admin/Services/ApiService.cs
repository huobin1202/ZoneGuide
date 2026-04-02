using System.Net.Http.Json;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Admin.Services;

public interface IApiService
{
    // POI Operations
    Task<List<POIDto>> GetPOIsAsync();
    Task<POIDto?> GetPOIAsync(string id);
    Task<POIDto?> CreatePOIAsync(CreatePOIDto dto);
    Task<POIDto?> UpdatePOIAsync(string id, UpdatePOIDto dto);
    Task<bool> DeletePOIAsync(string id);

    // Tour Operations
    Task<List<TourDto>> GetToursAsync();
    Task<TourDto?> GetTourAsync(string id);
    Task<TourWithPOIsDto?> GetTourWithDetailsAsync(string id);
    Task<TourDto?> CreateTourAsync(CreateTourDto dto);
    Task<TourDto?> UpdateTourAsync(string id, UpdateTourDto dto);
    Task<bool> DeleteTourAsync(string id);

    // Analytics
    Task<DashboardAnalyticsDto?> GetDashboardAsync(DateTime? from = null, DateTime? to = null);
    Task<List<TopPOIDto>> GetTopPOIsAsync(int count = 10);
    Task<List<HeatmapPointDto>> GetHeatmapDataAsync(DateTime? from = null, DateTime? to = null);

    // TTS
    Task<string?> GenerateTtsAsync(string text, string language);

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

    public async Task<List<TopPOIDto>> GetTopPOIsAsync(int count = 10)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TopPOIDto>>($"api/analytics/top-pois?count={count}") ?? new();
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
            var response = await _httpClient.PostAsJsonAsync("api/tts/generate", new { text, language });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return result.GetProperty("audioUrl").GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}

// Analytics DTOs are defined in ZoneGuide.API.Services.SyncService
// We use those definitions through the Shared project
