using SQLite;

namespace ZoneGuide.Shared.Models;

/// <summary>
/// Tour - Tuyến tham quan
/// </summary>
public class Tour
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Mã định danh duy nhất
    /// </summary>
    public string UniqueCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Tên tour
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả tour
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn file audio offline cho tour
    /// </summary>
    public string? AudioFilePath { get; set; }

    /// <summary>
    /// URL audio online cho tour
    /// </summary>
    public string? AudioUrl { get; set; }
    
    /// <summary>
    /// Thời gian ước tính (phút)
    /// </summary>
    public int EstimatedDurationMinutes { get; set; }
    
    /// <summary>
    /// Khoảng cách ước tính (mét)
    /// </summary>
    public double EstimatedDistanceMeters { get; set; }
    
    /// <summary>
    /// Số điểm POI trong tour
    /// </summary>
    public int POICount { get; set; }
    
    /// <summary>
    /// URL ảnh online
    /// </summary>
    public string? ThumbnailUrl { get; set; }
    
    /// <summary>
    /// Ngôn ngữ mặc định
    /// </summary>
    public string Language { get; set; } = "vi-VN";
    
    /// <summary>
    /// Có hỗ trợ xe lăn không
    /// </summary>
    public bool WheelchairAccessible { get; set; }
    
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
}
