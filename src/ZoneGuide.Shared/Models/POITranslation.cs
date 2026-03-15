using SQLite;

namespace ZoneGuide.Shared.Models;

/// <summary>
/// Bản dịch cho POI
/// </summary>
public class POITranslation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID của POI gốc
    /// </summary>
    [Indexed]
    public int POIId { get; set; }
    
    /// <summary>
    /// Mã ngôn ngữ (vi-VN, en-US, zh-CN, ja-JP, ko-KR, fr-FR, etc.)
    /// </summary>
    [Indexed]
    public string LanguageCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Tên điểm (đã dịch)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả ngắn (đã dịch)
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả đầy đủ (đã dịch)
    /// </summary>
    public string FullDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Script TTS (đã dịch)
    /// </summary>
    public string? TTSScript { get; set; }
    
    /// <summary>
    /// Đường dẫn file audio (theo ngôn ngữ)
    /// </summary>
    public string? AudioFilePath { get; set; }
    
    /// <summary>
    /// URL audio online
    /// </summary>
    public string? AudioUrl { get; set; }
    
    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Thời gian cập nhật
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
