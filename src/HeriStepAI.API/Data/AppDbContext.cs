using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<POIEntity> POIs { get; set; }
    public DbSet<POITranslationEntity> POITranslations { get; set; }
    public DbSet<TourEntity> Tours { get; set; }
    public DbSet<LocationHistoryEntity> LocationHistories { get; set; }
    public DbSet<NarrationHistoryEntity> NarrationHistories { get; set; }
    public DbSet<POIStatisticsEntity> POIStatistics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // POI
        modelBuilder.Entity<POIEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UniqueCode).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UniqueCode).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Tour)
                  .WithMany(t => t.POIs)
                  .HasForeignKey(e => e.TourId);
        });

        // POI Translation
        modelBuilder.Entity<POITranslationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.POIId, e.LanguageCode }).IsUnique();
            entity.HasOne(e => e.POI)
                  .WithMany(p => p.Translations)
                  .HasForeignKey(e => e.POIId);
        });

        // Tour
        modelBuilder.Entity<TourEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UniqueCode).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        // Location History
        modelBuilder.Entity<LocationHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AnonymousDeviceId);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Narration History
        modelBuilder.Entity<NarrationHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AnonymousDeviceId);
            entity.HasIndex(e => e.POIId);
            entity.HasIndex(e => e.StartTime);
        });

        // POI Statistics
        modelBuilder.Entity<POIStatisticsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.POIId, e.Date }).IsUnique();
        });
    }
}

#region Entities

public class POIEntity
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadius { get; set; } = 50;
    public double ApproachRadius { get; set; } = 100;
    public int Priority { get; set; } = 5;
    public string? AudioFilePath { get; set; }
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string Language { get; set; } = "vi-VN";
    public int? TourId { get; set; }
    public int OrderInTour { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int CooldownSeconds { get; set; } = 300;

    public TourEntity? Tour { get; set; }
    public ICollection<POITranslationEntity> Translations { get; set; } = new List<POITranslationEntity>();
}

public class POITranslationEntity
{
    public int Id { get; set; }
    public int POIId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string? TTSScript { get; set; }
    public string? AudioFilePath { get; set; }
    public string? AudioUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public POIEntity POI { get; set; } = null!;
}

public class TourEntity
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public double EstimatedDistanceMeters { get; set; }
    public int POICount { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Language { get; set; } = "vi-VN";
    public int DifficultyLevel { get; set; } = 1;
    public bool WheelchairAccessible { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<POIEntity> POIs { get; set; } = new List<POIEntity>();
}

public class LocationHistoryEntity
{
    public int Id { get; set; }
    public string AnonymousDeviceId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public double? Altitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NarrationHistoryEntity
{
    public int Id { get; set; }
    public string AnonymousDeviceId { get; set; } = string.Empty;
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

public class POIStatisticsEntity
{
    public int Id { get; set; }
    public int POIId { get; set; }
    public DateTime Date { get; set; }
    public int ListenCount { get; set; }
    public int CompletedCount { get; set; }
    public long TotalListenDurationSeconds { get; set; }
    public int UniqueUsers { get; set; }
}

#endregion
