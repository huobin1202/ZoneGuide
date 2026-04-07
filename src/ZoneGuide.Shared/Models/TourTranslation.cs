using SQLite;

namespace ZoneGuide.Shared.Models;

/// <summary>
/// Ban dich mo ta cho Tour
/// </summary>
public class TourTranslation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int TourId { get; set; }

    [Indexed]
    public string LanguageCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsOutdated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
