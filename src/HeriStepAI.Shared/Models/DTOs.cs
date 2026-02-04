namespace HeriStepAI.Shared.Models;

/// <summary>
/// DTO cho API Response
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };
    
    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };
}

/// <summary>
/// DTO cho POI
/// </summary>
public class POIDto
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadius { get; set; }
    public double ApproachRadius { get; set; }
    public int Priority { get; set; }
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string Language { get; set; } = "vi-VN";
    public int? TourId { get; set; }
    public int OrderInTour { get; set; }
    public int CooldownSeconds { get; set; }
    public bool IsActive { get; set; }
    public List<POITranslationDto>? Translations { get; set; }
}

/// <summary>
/// DTO cho POI Translation
/// </summary>
public class POITranslationDto
{
    public int Id { get; set; }
    public int POIId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string? TTSScript { get; set; }
    public string? AudioUrl { get; set; }
}

/// <summary>
/// DTO cho Tour
/// </summary>
public class TourDto
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public double EstimatedDistanceMeters { get; set; }
    public int POICount { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Language { get; set; } = "vi-VN";
    public int DifficultyLevel { get; set; }
    public bool WheelchairAccessible { get; set; }
    public bool IsActive { get; set; }
    public List<POIDto>? POIs { get; set; }
}

/// <summary>
/// DTO cho đồng bộ dữ liệu
/// </summary>
public class SyncDataDto
{
    public DateTime LastSyncTime { get; set; }
    public List<POIDto> POIs { get; set; } = new();
    public List<TourDto> Tours { get; set; } = new();
    public List<POITranslationDto> Translations { get; set; } = new();
    public List<int> DeletedPOIIds { get; set; } = new();
    public List<int> DeletedTourIds { get; set; } = new();
}

/// <summary>
/// Request đồng bộ
/// </summary>
public class SyncRequest
{
    public DateTime? LastSyncTime { get; set; }
    public string? DeviceId { get; set; }
    public string? Language { get; set; }
}

/// <summary>
/// DTO cho Analytics
/// </summary>
public class AnalyticsUploadDto
{
    public string AnonymousDeviceId { get; set; } = string.Empty;
    public List<LocationHistoryDto> Locations { get; set; } = new();
    public List<NarrationHistoryDto> Narrations { get; set; } = new();
}

public class LocationHistoryDto
{
    public string SessionId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public double? Altitude { get; set; }
    public DateTime Timestamp { get; set; }
}

public class NarrationHistoryDto
{
    public string SessionId { get; set; } = string.Empty;
    public int POIId { get; set; }
    public string POIName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool Completed { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public double TriggerDistance { get; set; }
    public double TriggerLatitude { get; set; }
    public double TriggerLongitude { get; set; }
}

/// <summary>
/// DTO cho Dashboard Analytics
/// </summary>
public class DashboardAnalyticsDto
{
    public int TotalPOIs { get; set; }
    public int TotalTours { get; set; }
    public int TotalListens { get; set; }
    public int UniqueUsers { get; set; }
    public double AverageListenDurationSeconds { get; set; }
    public double CompletionRate { get; set; }
    public List<TopPOIDto> TopPOIs { get; set; } = new();
    public List<HeatmapPointDto> HeatmapData { get; set; } = new();
    public List<DailyStatsDto> DailyStats { get; set; } = new();
}

public class TopPOIDto
{
    public int POIId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ListenCount { get; set; }
    public double AvgDurationSeconds { get; set; }
    public double CompletionRate { get; set; }
}

public class HeatmapPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Weight { get; set; }
}

public class DailyStatsDto
{
    public DateTime Date { get; set; }
    public int ListenCount { get; set; }
    public int UniqueUsers { get; set; }
    public double AvgDurationSeconds { get; set; }
}
