using HeriStepAI.Shared.Models;

namespace HeriStepAI.Shared.Interfaces;

/// <summary>
/// Interface cho POI Repository
/// </summary>
public interface IPOIRepository
{
    Task<List<POI>> GetAllAsync();
    Task<POI?> GetByIdAsync(int id);
    Task<POI?> GetByCodeAsync(string code);
    Task<List<POI>> GetByTourIdAsync(int tourId);
    Task<List<POI>> GetActiveAsync();
    Task<List<POI>> SearchAsync(string keyword);
    Task<int> InsertAsync(POI poi);
    Task<int> UpdateAsync(POI poi);
    Task<int> DeleteAsync(int id);
    Task<int> InsertOrUpdateAsync(POI poi);
    Task<List<POI>> GetByLocationAsync(double lat, double lon, double radiusMeters);
}

/// <summary>
/// Interface cho POI Translation Repository
/// </summary>
public interface IPOITranslationRepository
{
    Task<List<POITranslation>> GetByPOIIdAsync(int poiId);
    Task<POITranslation?> GetByPOIIdAndLanguageAsync(int poiId, string languageCode);
    Task<int> InsertAsync(POITranslation translation);
    Task<int> UpdateAsync(POITranslation translation);
    Task<int> DeleteAsync(int id);
    Task<int> InsertOrUpdateAsync(POITranslation translation);
}

/// <summary>
/// Interface cho Tour Repository
/// </summary>
public interface ITourRepository
{
    Task<List<Tour>> GetAllAsync();
    Task<Tour?> GetByIdAsync(int id);
    Task<Tour?> GetByCodeAsync(string code);
    Task<List<Tour>> GetActiveAsync();
    Task<List<Tour>> SearchAsync(string keyword);
    Task<int> InsertAsync(Tour tour);
    Task<int> UpdateAsync(Tour tour);
    Task<int> DeleteAsync(int id);
    Task<int> InsertOrUpdateAsync(Tour tour);
}

/// <summary>
/// Interface cho Analytics Repository
/// </summary>
public interface IAnalyticsRepository
{
    // Location History
    Task<int> InsertLocationAsync(LocationHistory location);
    Task<int> InsertLocationsAsync(IEnumerable<LocationHistory> locations);
    Task<List<LocationHistory>> GetLocationsBySessionAsync(string sessionId);
    Task<List<LocationHistory>> GetLocationsByDateRangeAsync(DateTime start, DateTime end);
    Task<int> DeleteOldLocationsAsync(DateTime olderThan);
    
    // Narration History
    Task<int> InsertNarrationAsync(NarrationHistory narration);
    Task<int> UpdateNarrationAsync(NarrationHistory narration);
    Task<List<NarrationHistory>> GetNarrationsBySessionAsync(string sessionId);
    Task<List<NarrationHistory>> GetNarrationsByPOIAsync(int poiId);
    Task<List<NarrationHistory>> GetNarrationsByDateRangeAsync(DateTime start, DateTime end);
    
    // POI Statistics
    Task<POIStatistics?> GetStatisticsAsync(int poiId, DateTime date);
    Task<int> InsertOrUpdateStatisticsAsync(POIStatistics stats);
    Task<List<POIStatistics>> GetStatisticsByPOIAsync(int poiId, DateTime start, DateTime end);
    Task<List<POIStatistics>> GetTopPOIsAsync(DateTime start, DateTime end, int count = 10);
}

/// <summary>
/// Interface cho Sync Service
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Sự kiện khi đồng bộ bắt đầu
    /// </summary>
    event EventHandler? SyncStarted;
    
    /// <summary>
    /// Sự kiện khi đồng bộ hoàn thành
    /// </summary>
    event EventHandler<bool>? SyncCompleted;
    
    /// <summary>
    /// Sự kiện cập nhật tiến trình
    /// </summary>
    event EventHandler<double>? SyncProgress;
    
    /// <summary>
    /// Đang đồng bộ
    /// </summary>
    bool IsSyncing { get; }
    
    /// <summary>
    /// Thời gian đồng bộ cuối
    /// </summary>
    DateTime? LastSyncTime { get; }
    
    /// <summary>
    /// Đồng bộ dữ liệu từ server
    /// </summary>
    Task<bool> SyncFromServerAsync();
    
    /// <summary>
    /// Upload analytics lên server
    /// </summary>
    Task<bool> UploadAnalyticsAsync();
    
    /// <summary>
    /// Tải nội dung offline cho tour
    /// </summary>
    Task<bool> DownloadTourOfflineAsync(int tourId);
    
    /// <summary>
    /// Xóa nội dung offline cho tour
    /// </summary>
    Task<bool> DeleteTourOfflineAsync(int tourId);
    
    /// <summary>
    /// Kiểm tra nội dung offline có sẵn
    /// </summary>
    Task<bool> IsTourOfflineAvailableAsync(int tourId);
}

/// <summary>
/// Interface cho Settings Service
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
    Task RemoveAsync(string key);
}

/// <summary>
/// Interface cho Audio Service
/// </summary>
public interface IAudioService
{
    event EventHandler? PlaybackStarted;
    event EventHandler? PlaybackCompleted;
    event EventHandler? PlaybackPaused;
    event EventHandler<double>? ProgressChanged;
    event EventHandler<string>? PlaybackError;
    
    bool IsPlaying { get; }
    bool IsPaused { get; }
    double CurrentPosition { get; }
    double Duration { get; }
    
    Task PlayAsync(string filePath);
    Task PlayFromUrlAsync(string url);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SeekAsync(double position);
    void SetVolume(float volume);
}
