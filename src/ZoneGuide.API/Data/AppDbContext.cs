using Microsoft.EntityFrameworkCore;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<POIEntity> POIs { get; set; }
    public DbSet<POITranslationEntity> POITranslations { get; set; }
    public DbSet<TourEntity> Tours { get; set; }
    public DbSet<TourPOIEntity> TourPOIs { get; set; }
    public DbSet<LocationHistoryEntity> LocationHistories { get; set; }
    public DbSet<NarrationHistoryEntity> NarrationHistories { get; set; }
    public DbSet<POIStatisticsEntity> POIStatistics { get; set; }
    public DbSet<DeletedRecordEntity> DeletedRecords { get; set; }
    
    // User & Contribution
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<POIContributionEntity> POIContributions { get; set; }
    public DbSet<POIApprovalHistoryEntity> POIApprovalHistories { get; set; }
    public DbSet<ActivityLogEntity> ActivityLogs { get; set; }

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
            entity.Property(e => e.Address).HasMaxLength(500);
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

        // User
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
        });

        // POI Contribution
        modelBuilder.Entity<POIContributionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ContributorId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Contributor)
                  .WithMany()
                  .HasForeignKey(e => e.ContributorId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Reviewer)
                  .WithMany()
                  .HasForeignKey(e => e.ReviewerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // POI Approval History
        modelBuilder.Entity<POIApprovalHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ContributionId);
            entity.HasOne(e => e.Contribution)
                  .WithMany()
                  .HasForeignKey(e => e.ContributionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ActionBy)
                  .WithMany()
                  .HasForeignKey(e => e.ActionById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Activity Log
        modelBuilder.Entity<ActivityLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

#region Entities

public class POIEntity
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadius { get; set; } = 50;
    public double ApproachRadius { get; set; } = 100;
    public int Priority { get; set; } = 5;
    public string? Category { get; set; }
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
    public string? AudioUrl { get; set; }
    public bool IsOutdated { get; set; }
    public bool IsAudioOutdated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public POIEntity POI { get; set; } = null!;
}

public class TourEntity
{
    public int Id { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public double DistanceKm { get; set; }
    public double EstimatedDistanceMeters { get; set; }
    public int POICount { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Language { get; set; } = "vi-VN";
    public string Difficulty { get; set; } = "Easy";
    public int DifficultyLevel { get; set; } = 1;
    public bool WheelchairAccessible { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<POIEntity> POIs { get; set; } = new List<POIEntity>();
    public ICollection<TourPOIEntity> POIIds { get; set; } = new List<TourPOIEntity>();
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
    public string POIId { get; set; } = string.Empty;
    public string POIName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public bool Completed { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public double? TriggerDistance { get; set; }
    public double? TriggerLatitude { get; set; }
    public double? TriggerLongitude { get; set; }
}

public class POIStatisticsEntity
{
    public int Id { get; set; }
    public string POIId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int ListenCount { get; set; }
    public int CompletedCount { get; set; }
    public long TotalListenDurationSeconds { get; set; }
    public int UniqueUsers { get; set; }
}

public class TourPOIEntity
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int POIId { get; set; }
    public int Order { get; set; }
    public TourEntity Tour { get; set; } = null!;
    public POIEntity POI { get; set; } = null!;
}

public class DeletedRecordEntity
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}

public class UserEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
}

public class POIContributionEntity
{
    public int Id { get; set; }
    public int? OriginalPOIId { get; set; }
    public int ContributorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadius { get; set; } = 50;
    public double ApproachRadius { get; set; } = 100;
    public int Priority { get; set; } = 5;
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string Language { get; set; } = "vi-VN";
    public string? Category { get; set; }
    public POIApprovalStatus Status { get; set; } = POIApprovalStatus.Draft;
    public string? ContributorNotes { get; set; }
    public string? ReviewerFeedback { get; set; }
    public int? ReviewerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    
    public UserEntity Contributor { get; set; } = null!;
    public UserEntity? Reviewer { get; set; }
}

public class POIApprovalHistoryEntity
{
    public int Id { get; set; }
    public int ContributionId { get; set; }
    public POIApprovalStatus OldStatus { get; set; }
    public POIApprovalStatus NewStatus { get; set; }
    public string? Notes { get; set; }
    public int ActionById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public POIContributionEntity Contribution { get; set; } = null!;
    public UserEntity ActionBy { get; set; } = null!;
}

public class ActivityLogEntity
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Details { get; set; }
    public int? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserEntity? User { get; set; }
}

#endregion
