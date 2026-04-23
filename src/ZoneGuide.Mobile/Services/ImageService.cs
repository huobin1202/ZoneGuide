using System.Diagnostics;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service quản lý caching ảnh để tối ưu performance
/// </summary>
public class ImageService : IImageService, IDisposable
{
    private const long MaxCacheSizeBytes = 50 * 1024 * 1024; // 50MB limit
    private const string ImageCacheDir = "image_cache";
    private const string CacheIndexFile = "image_cache_index.txt";

    private readonly HttpClient _httpClient = new();
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, string> _memoryCache = new(); // URL -> file path
    private readonly object _cacheLock = new();
    private long _currentCacheSizeBytes;

    public ImageService()
    {
        _cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, ImageCacheDir);
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }

        LoadCacheIndex();
    }

    /// <summary>
    /// Lấy đường dẫn file ảnh (từ cache nếu có, tải về nếu chưa có)
    /// </summary>
    public async Task<string?> GetImageAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        // Kiểm tra cache
        var cachedPath = GetCachedImagePath(imageUrl);
        if (cachedPath != null)
        {
            return cachedPath;
        }

        // Tải về và cache
        try
        {
            var fileExtension = Path.GetExtension(imageUrl)?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ".jpg",
                ".png" => ".png",
                ".gif" => ".gif",
                ".webp" => ".webp",
                _ => ".jpg"
            };

            var urlHash = Convert.ToBase64String(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(imageUrl)));
            var cacheFileName = $"{urlHash}{fileExtension}";
            var cacheFilePath = Path.Combine(_cacheDirectory, cacheFileName);

            using var response = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var networkStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(cacheFilePath);
            await networkStream.CopyToAsync(fileStream);

            AddToCache(imageUrl, cacheFilePath);
            return cacheFilePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageService] Failed to download image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Xóa một ảnh khỏi cache
    /// </summary>
    public void RemoveFromCache(string imageUrl)
    {
        lock (_cacheLock)
        {
            if (_memoryCache.TryGetValue(imageUrl, out var filePath))
            {
                RemoveCachedFile(filePath);
                _memoryCache.Remove(imageUrl);
                SaveCacheIndex();
            }
        }
    }

    /// <summary>
    /// Xóa toàn bộ cache ảnh
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var filePath in _memoryCache.Values)
            {
                RemoveCachedFile(filePath);
            }
            _memoryCache.Clear();
            _currentCacheSizeBytes = 0;
            SaveCacheIndex();
        }
    }

    /// <summary>
    /// Lấy dung lượng cache hiện tại
    /// </summary>
    public long GetCacheSizeBytes()
    {
        lock (_cacheLock)
        {
            return _currentCacheSizeBytes;
        }
    }

    private void LoadCacheIndex()
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, CacheIndexFile);
            if (File.Exists(indexPath))
            {
                var lines = File.ReadAllLines(indexPath);
                lock (_cacheLock)
                {
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            var url = parts[0];
                            var filePath = parts[1];
                            if (File.Exists(filePath))
                            {
                                _memoryCache[url] = filePath;
                                _currentCacheSizeBytes += new FileInfo(filePath).Length;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore cache load errors
        }
    }

    private void SaveCacheIndex()
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, CacheIndexFile);
            var lines = new List<string>();
            lock (_cacheLock)
            {
                foreach (var kvp in _memoryCache)
                {
                    lines.Add($"{kvp.Key}|{kvp.Value}");
                }
            }
            File.WriteAllLines(indexPath, lines);
        }
        catch
        {
            // Ignore cache save errors
        }
    }

    private string? GetCachedImagePath(string url)
    {
        lock (_cacheLock)
        {
            if (_memoryCache.TryGetValue(url, out var cachedPath) && File.Exists(cachedPath))
            {
                return cachedPath;
            }
        }
        return null;
    }

    private void AddToCache(string url, string filePath)
    {
        lock (_cacheLock)
        {
            // Nếu đã cache rồi thì xóa cái cũ
            if (_memoryCache.ContainsKey(url))
            {
                RemoveFromCache(url);
            }

            var fileSize = new FileInfo(filePath).Length;

            // Kiểm tra kích thước cache, xóa các file cũ nếu cần (FIFO)
            while (_currentCacheSizeBytes + fileSize > MaxCacheSizeBytes && _memoryCache.Count > 0)
            {
                var firstKey = _memoryCache.Keys.First();
                RemoveFromCache(firstKey);
            }

            _memoryCache[url] = filePath;
            _currentCacheSizeBytes += fileSize;
        }

        SaveCacheIndex();
    }

    private void RemoveCachedFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                _currentCacheSizeBytes -= fileInfo.Length;
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore file deletion errors
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Interface cho ImageService
/// </summary>
public interface IImageService
{
    Task<string?> GetImageAsync(string? imageUrl);
    void RemoveFromCache(string imageUrl);
    void ClearCache();
    long GetCacheSizeBytes();
}
