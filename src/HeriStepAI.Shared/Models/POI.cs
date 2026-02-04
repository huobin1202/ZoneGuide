using SQLite;

namespace HeriStepAI.Shared.Models;

/// <summary>
/// Point of Interest - Điểm thuyết minh
/// </summary>
public class POI
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Mã định danh duy nhất
    /// </summary>
    public string UniqueCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Tên điểm
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả ngắn
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả đầy đủ
    /// </summary>
    public string FullDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Vĩ độ
    /// </summary>
    public double Latitude { get; set; }
    
    /// <summary>
    /// Kinh độ
    /// </summary>
    public double Longitude { get; set; }
    
    /// <summary>
    /// Bán kính kích hoạt (mét)
    /// </summary>
    public double TriggerRadius { get; set; } = 50;
    
    /// <summary>
    /// Bán kính cảnh báo đến gần (mét)
    /// </summary>
    public double ApproachRadius { get; set; } = 100;
    
    /// <summary>
    /// Mức độ ưu tiên (1-10, cao hơn = ưu tiên hơn)
    /// </summary>
    public int Priority { get; set; } = 5;
    
    /// <summary>
    /// Đường dẫn file audio offline
    /// </summary>
    public string? AudioFilePath { get; set; }
    
    /// <summary>
    /// URL audio online
    /// </summary>
    public string? AudioUrl { get; set; }
    
    /// <summary>
    /// Script TTS (nếu không có file audio)
    /// </summary>
    public string? TTSScript { get; set; }
    
    /// <summary>
    /// Đường dẫn ảnh minh họa
    /// </summary>
    public string? ImagePath { get; set; }
    
    /// <summary>
    /// URL ảnh online
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Link bản đồ (Google Maps, Apple Maps, etc.)
    /// </summary>
    public string? MapLink { get; set; }
    
    /// <summary>
    /// Ngôn ngữ mặc định
    /// </summary>
    public string Language { get; set; } = "vi-VN";
    
    /// <summary>
    /// ID Tour (nếu thuộc tour cụ thể)
    /// </summary>
    public int? TourId { get; set; }
    
    /// <summary>
    /// Thứ tự trong tour
    /// </summary>
    public int OrderInTour { get; set; }
    
    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Thời gian cập nhật
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Trạng thái hoạt động
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Cooldown giữa các lần phát (giây)
    /// </summary>
    public int CooldownSeconds { get; set; } = 300; // 5 phút
    
    /// <summary>
    /// Tính khoảng cách từ vị trí hiện tại đến POI (mét)
    /// </summary>
    public double CalculateDistance(double currentLat, double currentLon)
    {
        const double R = 6371000; // Bán kính Trái Đất (mét)
        
        var lat1 = currentLat * Math.PI / 180;
        var lat2 = Latitude * Math.PI / 180;
        var deltaLat = (Latitude - currentLat) * Math.PI / 180;
        var deltaLon = (Longitude - currentLon) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }
}
