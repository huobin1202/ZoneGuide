namespace ZoneGuide.Shared.Models;

/// <summary>
/// Sự kiện Geofence
/// </summary>
public class GeofenceEvent
{
    /// <summary>
    /// POI liên quan
    /// </summary>
    public POI POI { get; set; } = null!;
    
    /// <summary>
    /// Loại sự kiện
    /// </summary>
    public GeofenceEventType EventType { get; set; }
    
    /// <summary>
    /// Khoảng cách hiện tại đến POI (mét)
    /// </summary>
    public double Distance { get; set; }
    
    /// <summary>
    /// Thời gian sự kiện
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Vị trí khi trigger
    /// </summary>
    public LocationData Location { get; set; } = null!;
}

/// <summary>
/// Loại sự kiện Geofence
/// </summary>
public enum GeofenceEventType
{
    /// <summary>
    /// Đi vào vùng trigger
    /// </summary>
    Enter,
    
    /// <summary>
    /// Đến gần (trong vùng approach)
    /// </summary>
    Approach,
    
    /// <summary>
    /// Rời khỏi vùng
    /// </summary>
    Exit,
    
    /// <summary>
    /// Ở trong vùng (dwell)
    /// </summary>
    Dwell
}

/// <summary>
/// Dữ liệu vị trí
/// </summary>
public class LocationData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public double? Altitude { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Tính khoảng cách đến một điểm khác (mét)
    /// </summary>
    public double DistanceTo(double lat, double lon)
    {
        const double R = 6371000; // Bán kính Trái Đất (mét)
        
        var lat1 = Latitude * Math.PI / 180;
        var lat2 = lat * Math.PI / 180;
        var deltaLat = (lat - Latitude) * Math.PI / 180;
        var deltaLon = (lon - Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }
}

/// <summary>
/// Item trong hàng đợi phát audio
/// </summary>
public class NarrationQueueItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public POI POI { get; set; } = null!;
    public string? AudioPath { get; set; }
    public string? AudioUrl { get; set; }
    public string? TTSText { get; set; }
    public string Language { get; set; } = "vi-VN";
    public int Priority { get; set; }
    public GeofenceEventType TriggerType { get; set; }
    public double TriggerDistance { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public NarrationStatus Status { get; set; } = NarrationStatus.Queued;
}

/// <summary>
/// Trạng thái phát thuyết minh
/// </summary>
public enum NarrationStatus
{
    Queued,
    Playing,
    Paused,
    Completed,
    Cancelled,
    Error
}

/// <summary>
/// Cài đặt ứng dụng
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Đã hoàn thành bước chọn ngôn ngữ lần đầu hay chưa
    /// </summary>
    public bool HasCompletedLanguageSelection { get; set; }

    /// <summary>
    /// Ngôn ngữ ưa thích
    /// </summary>
    public string PreferredLanguage { get; set; } = "vi-VN";
    
    /// <summary>
    /// Giọng TTS ưa thích
    /// </summary>
    public string PreferredVoice { get; set; } = string.Empty;
    
    /// <summary>
    /// Tốc độ TTS (0.5 - 2.0)
    /// </summary>
    public float TTSSpeed { get; set; } = 1.0f;
    
    /// <summary>
    /// Âm lượng (0.0 - 1.0)
    /// </summary>
    public float Volume { get; set; } = 1.0f;
    
    /// <summary>
    /// Độ nhạy GPS (Low, Medium, High)
    /// </summary>
    public GPSAccuracyLevel GPSAccuracy { get; set; } = GPSAccuracyLevel.Medium;
    
    /// <summary>
    /// Bán kính trigger mặc định (mét)
    /// </summary>
    public double DefaultTriggerRadius { get; set; } = 50;
    
    /// <summary>
    /// Bán kính approach mặc định (mét)
    /// </summary>
    public double DefaultApproachRadius { get; set; } = 100;
    
    /// <summary>
    /// Cooldown mặc định giữa các lần phát (giây)
    /// </summary>
    public int DefaultCooldownSeconds { get; set; } = 300;
    
    /// <summary>
    /// Tự động phát khi đi vào vùng
    /// </summary>
    public bool AutoPlayOnEnter { get; set; } = true;
    
    /// <summary>
    /// Thông báo khi đến gần
    /// </summary>
    public bool NotifyOnApproach { get; set; } = true;
    
}

/// <summary>
/// Mức độ chính xác GPS
/// </summary>
public enum GPSAccuracyLevel
{
    /// <summary>
    /// Thấp - Tiết kiệm pin, cập nhật mỗi 30 giây
    /// </summary>
    Low,
    
    /// <summary>
    /// Trung bình - Cân bằng, cập nhật mỗi 10 giây
    /// </summary>
    Medium,
    
    /// <summary>
    /// Cao - Chính xác nhất, cập nhật mỗi 3 giây
    /// </summary>
    High
}
