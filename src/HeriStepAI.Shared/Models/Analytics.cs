using SQLite;

namespace HeriStepAI.Shared.Models;

/// <summary>
/// Lịch sử vị trí người dùng (ẩn danh)
/// </summary>
public class LocationHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID thiết bị ẩn danh (hash)
    /// </summary>
    [Indexed]
    public string AnonymousDeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID Session
    /// </summary>
    [Indexed]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Vĩ độ
    /// </summary>
    public double Latitude { get; set; }
    
    /// <summary>
    /// Kinh độ
    /// </summary>
    public double Longitude { get; set; }
    
    /// <summary>
    /// Độ chính xác (mét)
    /// </summary>
    public double Accuracy { get; set; }
    
    /// <summary>
    /// Tốc độ (m/s)
    /// </summary>
    public double? Speed { get; set; }
    
    /// <summary>
    /// Hướng di chuyển (độ)
    /// </summary>
    public double? Heading { get; set; }
    
    /// <summary>
    /// Độ cao (mét)
    /// </summary>
    public double? Altitude { get; set; }
    
    /// <summary>
    /// Thời gian ghi nhận
    /// </summary>
    [Indexed]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lịch sử nghe thuyết minh
/// </summary>
public class NarrationHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID thiết bị ẩn danh (hash)
    /// </summary>
    [Indexed]
    public string AnonymousDeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID Session
    /// </summary>
    [Indexed]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID POI
    /// </summary>
    [Indexed]
    public int POIId { get; set; }
    
    /// <summary>
    /// Tên POI (để tra cứu nhanh)
    /// </summary>
    public string POIName { get; set; } = string.Empty;
    
    /// <summary>
    /// Ngôn ngữ đã nghe
    /// </summary>
    public string Language { get; set; } = string.Empty;
    
    /// <summary>
    /// Thời gian bắt đầu nghe
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Thời gian kết thúc nghe
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Thời lượng nghe (giây)
    /// </summary>
    public int DurationSeconds { get; set; }
    
    /// <summary>
    /// Tổng thời lượng audio (giây)
    /// </summary>
    public int TotalDurationSeconds { get; set; }
    
    /// <summary>
    /// Đã nghe hoàn thành
    /// </summary>
    public bool Completed { get; set; }
    
    /// <summary>
    /// Loại trigger (Enter, Approach, Manual)
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;
    
    /// <summary>
    /// Khoảng cách khi trigger (mét)
    /// </summary>
    public double TriggerDistance { get; set; }
    
    /// <summary>
    /// Vĩ độ khi trigger
    /// </summary>
    public double TriggerLatitude { get; set; }
    
    /// <summary>
    /// Kinh độ khi trigger
    /// </summary>
    public double TriggerLongitude { get; set; }
}

/// <summary>
/// Thống kê POI
/// </summary>
public class POIStatistics
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID POI
    /// </summary>
    [Indexed]
    public int POIId { get; set; }
    
    /// <summary>
    /// Ngày thống kê
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Số lần được nghe
    /// </summary>
    public int ListenCount { get; set; }
    
    /// <summary>
    /// Số lần nghe hoàn thành
    /// </summary>
    public int CompletedCount { get; set; }
    
    /// <summary>
    /// Tổng thời gian nghe (giây)
    /// </summary>
    public long TotalListenDurationSeconds { get; set; }
    
    /// <summary>
    /// Số người dùng unique
    /// </summary>
    public int UniqueUsers { get; set; }
}
