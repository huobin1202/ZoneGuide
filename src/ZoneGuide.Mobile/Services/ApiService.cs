using ZoneGuide.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service gọi API backend
/// </summary>
public class ApiService
{
    private const string PreferredApiBaseUrlKey = "preferred_api_base_url";
    private readonly HttpClient _httpClient;
    private readonly List<string> _syncBaseUrls;
    
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

    private static List<string> BuildSyncBaseUrls()
    {
#if ANDROID
        var urls = new List<string>
        {
            "http://10.0.2.2:56042/api/",
            "https://10.0.2.2:56040/api/",
            $"http://{ServerIP}:{ServerPort}/api/"
        };
#else
        var urls = new List<string>
        {
            "https://localhost:56040/api/",
            "http://localhost:56042/api/"
        };
#endif

        return urls
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildServerRoot(string apiBaseUrl)
    {
        var uri = new Uri(apiBaseUrl, UriKind.Absolute);
        var leftPart = uri.GetLeftPart(UriPartial.Authority);
        return leftPart.TrimEnd('/');
    }

    private static Uri BuildAbsoluteUri(string apiBaseUrl, string relativePath)
    {
        var normalizedPath = relativePath.TrimStart('/');
        return new Uri(new Uri(apiBaseUrl), normalizedPath);
    }

    private static string ResolveMediaUrl(string url, string apiBaseUrl)
    {
        var raw = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var trimmed = raw.Replace('\\', '/');

        if (trimmed.StartsWith("~/", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            if (IsLoopbackHost(absoluteUri.Host))
            {
                var rebasedRoot = BuildServerRoot(apiBaseUrl);
                var pathAndQuery = absoluteUri.PathAndQuery;
                if (!string.IsNullOrWhiteSpace(pathAndQuery) && pathAndQuery.StartsWith("/", StringComparison.Ordinal))
                {
                    return $"{rebasedRoot}{pathAndQuery}";
                }
            }

            return absoluteUri.ToString();
        }

        if (Path.IsPathRooted(trimmed))
        {
            var candidates = new[] { "/uploads/", "/images/", "/media/" };
            foreach (var candidate in candidates)
            {
                var idx = trimmed.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    trimmed = trimmed[idx..];
                    break;
                }
            }
        }

        if (trimmed.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["wwwroot/".Length..];
        }

        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("images/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "/" + trimmed;
        }

        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var serverRoot = BuildServerRoot(apiBaseUrl);

        return $"{serverRoot}{trimmed}";
    }

    private static bool IsLoopbackHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(host, "10.0.2.2", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<T?> GetFromAnyBaseUrlAsync<T>(string relativePath)
        where T : class
    {
        foreach (var baseUrl in GetOrderedBaseUrls())
        {
            try
            {
                var requestUri = BuildAbsoluteUri(baseUrl, relativePath);
                var response = await _httpClient.GetAsync(requestUri);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var payload = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var wrapped = JsonSerializer.Deserialize<ApiResponse<T>>(payload, jsonOptions);
                if (wrapped?.Data != null)
                {
                    SavePreferredBaseUrl(baseUrl);
                    return wrapped.Data;
                }

                var raw = JsonSerializer.Deserialize<T>(payload, jsonOptions);
                if (raw != null)
                {
                    SavePreferredBaseUrl(baseUrl);
                    return raw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] GET {relativePath} via {baseUrl} failed: {ex.Message}");
            }
        }

        return default;
    }

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

        _syncBaseUrls = BuildSyncBaseUrls();
    }

    private IEnumerable<string> GetOrderedBaseUrls()
    {
        var preferred = Preferences.Get(PreferredApiBaseUrlKey, string.Empty);
        if (string.IsNullOrWhiteSpace(preferred))
            return _syncBaseUrls;

        return _syncBaseUrls
            .OrderBy(url => string.Equals(url, preferred, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }

    private static void SavePreferredBaseUrl(string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            Preferences.Set(PreferredApiBaseUrlKey, baseUrl);
        }
    }

    #region POI
    
    public async Task<List<POIDto>?> GetPOIsAsync()
    {
        return await GetFromAnyBaseUrlAsync<List<POIDto>>("pois");
    }

    public async Task<POIDto?> GetPOIAsync(int id)
    {
        return await GetFromAnyBaseUrlAsync<POIDto>($"pois/{id}");
    }

    #endregion

    #region Tour

    public async Task<List<TourDto>?> GetToursAsync()
    {
        return await GetFromAnyBaseUrlAsync<List<TourDto>>("tours");
    }

    public async Task<TourDto?> GetTourAsync(int id)
    {
        return await GetFromAnyBaseUrlAsync<TourDto>($"tours/{id}");
    }

    public async Task<TourDto?> GetTourDetailsAsync(int id)
    {
        var detail = await GetFromAnyBaseUrlAsync<TourWithPOIsDto>($"tours/{id}/details");
        if (detail != null)
            return detail;

        // Fallback cho backend cũ không có endpoint details.
        return await GetTourAsync(id);
    }

    #endregion

    #region Sync

    public async Task<SyncDataDto?> SyncDataAsync(SyncRequest request)
    {
        // Thử nhiều endpoint để tránh lỗi do khác môi trường (emulator/LAN/localhost)
        foreach (var syncBaseUrl in GetOrderedBaseUrls())
        {
            try
            {
                // API trả về SyncResponseDto trực tiếp (không wrap ApiResponse)
                // Gửi request tương thích với SyncRequestDto trên server
                var serverRequest = new
                {
                    LastSyncTime = request.LastSyncTime,
                    Language = request.Language,
                    IncludePOIs = true,
                    IncludeTours = true
                };

                var syncUri = new Uri(new Uri(syncBaseUrl), "sync");
                var response = await _httpClient.PostAsJsonAsync(syncUri, serverRequest);
                if (response.IsSuccessStatusCode)
                {
                    SavePreferredBaseUrl(syncBaseUrl);

                    var json = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[ApiService] Sync OK via {syncBaseUrl}. Response: {json[..Math.Min(json.Length, 500)]}");

                    var syncResponse = System.Text.Json.JsonSerializer.Deserialize<SyncResponseFromServer>(json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (syncResponse != null)
                    {
                        var normalizedPois = (syncResponse.POIs ?? new List<POIDto>())
                            .Select(poi => NormalizePoiMediaUrls(poi, syncBaseUrl))
                            .ToList();

                        return new SyncDataDto
                        {
                            LastSyncTime = syncResponse.SyncedAt,
                            POIs = normalizedPois,
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
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[ApiService] Sync failed via {syncBaseUrl}: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody[..Math.Min(errorBody.Length, 500)]}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] SyncDataAsync error via {syncBaseUrl}: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine("[ApiService] SyncDataAsync failed on all endpoints.");
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

    private static POIDto NormalizePoiMediaUrls(POIDto poi, string apiBaseUrl)
    {
        poi.AudioUrl = ResolveMediaUrl(poi.AudioUrl ?? string.Empty, apiBaseUrl);
        poi.ImageUrl = ResolveMediaUrl(poi.ImageUrl ?? string.Empty, apiBaseUrl);

        if (poi.Translations != null)
        {
            foreach (var translation in poi.Translations)
            {
                translation.AudioUrl = ResolveMediaUrl(translation.AudioUrl ?? string.Empty, apiBaseUrl);
            }
        }

        return poi;
    }

    #endregion

    #region Auth

    public async Task<AuthResponseDto> LoginAsync(LoginDto request)
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        string? lastError = null;

        foreach (var baseUrl in GetOrderedBaseUrls())
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var loginUri = new Uri(new Uri(baseUrl), "auth/login");
                var response = await _httpClient.PostAsJsonAsync(loginUri, request, cts.Token);
                var json = await response.Content.ReadAsStringAsync(cts.Token);

                var authResponse = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<AuthResponseDto>(json, serializerOptions);

                if (response.IsSuccessStatusCode && authResponse?.Success == true)
                {
                    SavePreferredBaseUrl(baseUrl);
                    System.Diagnostics.Debug.WriteLine($"[ApiService] Login OK via {baseUrl}");
                    return authResponse;
                }

                if (authResponse != null && !string.IsNullOrWhiteSpace(authResponse.Message))
                {
                    lastError = authResponse.Message;
                }
                else
                {
                    lastError = $"Đăng nhập thất bại ({(int)response.StatusCode})";
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[ApiService] Login failed via {baseUrl}: {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (OperationCanceledException)
            {
                lastError = "Kết nối máy chủ quá chậm. Vui lòng thử lại.";
                System.Diagnostics.Debug.WriteLine($"[ApiService] Login timeout via {baseUrl}");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[ApiService] Login error via {baseUrl}: {ex.Message}");
            }
        }

        return new AuthResponseDto
        {
            Success = false,
            Message = string.IsNullOrWhiteSpace(lastError)
                ? "Không thể kết nối máy chủ để đăng nhập"
                : lastError
        };
    }

    #endregion

    #region Analytics

    public async Task<bool> UploadAnalyticsAsync(AnalyticsUploadDto data)
    {
        foreach (var baseUrl in GetOrderedBaseUrls())
        {
            try
            {
                var uploadUri = new Uri(new Uri(baseUrl), "analytics/upload");
                var response = await _httpClient.PostAsJsonAsync(uploadUri, data);
                if (response.IsSuccessStatusCode)
                {
                    SavePreferredBaseUrl(baseUrl);
                    return true;
                }

                var body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[ApiService] UploadAnalytics failed via {baseUrl}: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body[..Math.Min(body.Length, 300)]}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] UploadAnalytics error via {baseUrl}: {ex.Message}");
            }
        }

        return false;
    }

    #endregion

    #region Download

    public async Task<byte[]?> DownloadAudioAsync(string url)
    {
        foreach (var baseUrl in GetOrderedBaseUrls())
        {
            try
            {
                var absoluteUrl = ResolveMediaUrl(url, baseUrl);
                if (string.IsNullOrWhiteSpace(absoluteUrl))
                    continue;

                var bytes = await _httpClient.GetByteArrayAsync(absoluteUrl);
                if (bytes.Length > 0)
                {
                    SavePreferredBaseUrl(baseUrl);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] DownloadAudioAsync failed via {baseUrl}: {ex.Message}");
            }
        }

        return null;
    }

    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        foreach (var baseUrl in GetOrderedBaseUrls())
        {
            try
            {
                var absoluteUrl = ResolveMediaUrl(url, baseUrl);
                if (string.IsNullOrWhiteSpace(absoluteUrl))
                    continue;

                var bytes = await _httpClient.GetByteArrayAsync(absoluteUrl);
                if (bytes.Length > 0)
                {
                    SavePreferredBaseUrl(baseUrl);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] DownloadImageAsync failed via {baseUrl}: {ex.Message}");
            }
        }

        return null;
    }

    #endregion
}
