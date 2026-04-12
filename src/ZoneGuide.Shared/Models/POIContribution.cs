using SQLite;

namespace ZoneGuide.Shared.Models;

/// <summary>
/// Trạng thái duyệt POI
/// </summary>
public enum POIApprovalStatus
{
    /// <summary>
    /// Bản nháp - chưa gửi duyệt
    /// </summary>
    Draft = 0,
    
    /// <summary>
    /// Đang chờ duyệt
    /// </summary>
    Pending = 1,
    
    /// <summary>
    /// Đã duyệt
    /// </summary>
    Approved = 2,
    
    /// <summary>
    /// Bị từ chối
    /// </summary>
    Rejected = 3,
    
    /// <summary>
    /// Yêu cầu chỉnh sửa
    /// </summary>
    NeedsRevision = 4
}

/// <summary>
/// POI đóng góp từ Contributor - chờ duyệt
/// </summary>
public class POIContribution
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID của POI gốc (nếu là edit, null nếu là tạo mới)
    /// </summary>
    public int? OriginalPOIId { get; set; }
    
    /// <summary>
    /// ID người đóng góp
    /// </summary>
    public int ContributorId { get; set; }
    
    /// <summary>
    /// Tên điểm
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Vĩ độ
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Kinh độ
    /// </summary>
    public double? Longitude { get; set; }    /// <summary>
    /// Bán kính kích hoạt (mét)
    /// </summary>
    public double TriggerRadius { get; set; } = 50;
    
    /// <summary>
    /// Bán kính cảnh báo đến gần (mét)
    /// </summary>
    public double ApproachRadius { get; set; } = 100;
    
    /// <summary>
    /// Mức độ ưu tiên (1-10)
    /// </summary>
    public int Priority { get; set; } = 5;
    
    /// <summary>
    /// URL audio online
    /// </summary>
    public string? AudioUrl { get; set; }
    
    /// <summary>
    /// Script TTS
    /// </summary>
    public string? TTSScript { get; set; }
    
    /// <summary>
    /// URL ảnh
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Link bản đồ
    /// </summary>
    public string? MapLink { get; set; }
    
    /// <summary>
    /// Ngôn ngữ
    /// </summary>
    public string Language { get; set; } = "vi-VN";
    
    /// <summary>
    /// Danh mục
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Trạng thái duyệt
    /// </summary>
    public POIApprovalStatus Status { get; set; } = POIApprovalStatus.Draft;
    
    /// <summary>
    /// Ghi chú từ contributor
    /// </summary>
    public string? ContributorNotes { get; set; }
    
    /// <summary>
    /// Phản hồi từ reviewer
    /// </summary>
    public string? ReviewerFeedback { get; set; }
    
    /// <summary>
    /// ID người duyệt
    /// </summary>
    public int? ReviewerId { get; set; }
    
    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Thời gian cập nhật
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Thời gian gửi duyệt
    /// </summary>
    public DateTime? SubmittedAt { get; set; }
    
    /// <summary>
    /// Thời gian duyệt
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Lịch sử duyệt POI
/// </summary>
public class POIApprovalHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// ID POI Contribution
    /// </summary>
    public int ContributionId { get; set; }
    
    /// <summary>
    /// Trạng thái cũ
    /// </summary>
    public POIApprovalStatus OldStatus { get; set; }
    
    /// <summary>
    /// Trạng thái mới
    /// </summary>
    public POIApprovalStatus NewStatus { get; set; }
    
    /// <summary>
    /// Ghi chú
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// ID người thực hiện
    /// </summary>
    public int ActionById { get; set; }
    
    /// <summary>
    /// Thời gian
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

#region DTOs

/// <summary>
/// DTO tạo POI Contribution
/// </summary>
public class CreatePOIContributionDto
{
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double TriggerRadius { get; set; } = 50;
    public double ApproachRadius { get; set; } = 100;
    public int Priority { get; set; } = 5;
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? Category { get; set; }
    public string Language { get; set; } = "vi-VN";
    public string? ContributorNotes { get; set; }
    public int? OriginalPOIId { get; set; }
}

/// <summary>
/// DTO cập nhật POI Contribution
/// </summary>
public class UpdatePOIContributionDto
{
    public string? Name { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? TriggerRadius { get; set; }
    public double? ApproachRadius { get; set; }
    public int? Priority { get; set; }
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? Category { get; set; }
    public string? ContributorNotes { get; set; }
}

/// <summary>
/// DTO POI Contribution
/// </summary>
public class POIContributionDto
{
    public int Id { get; set; }
    public int? OriginalPOIId { get; set; }
    public int ContributorId { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double TriggerRadius { get; set; }
    public double ApproachRadius { get; set; }
    public int Priority { get; set; }
    public string? AudioUrl { get; set; }
    public string? TTSScript { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? Category { get; set; }
    public string Language { get; set; } = "vi-VN";
    public string Status { get; set; } = "Draft";
    public string? ContributorNotes { get; set; }
    public string? ReviewerFeedback { get; set; }
    public int? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// DTO duyệt/từ chối POI
/// </summary>
public class ReviewPOIContributionDto
{
    public int ContributionId { get; set; }
    public POIApprovalStatus Decision { get; set; }
    public string? Feedback { get; set; }
}

/// <summary>
/// DTO lịch sử duyệt
/// </summary>
public class POIApprovalHistoryDto
{
    public int Id { get; set; }
    public int ContributionId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int ActionById { get; set; }
    public string ActionByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Thống kê đóng góp
/// </summary>
public class ContributionStatsDto
{
    public int TotalContributions { get; set; }
    public int DraftCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int NeedsRevisionCount { get; set; }
}

/// <summary>
/// Dashboard contributor
/// </summary>
public class ContributorDashboardDto
{
    public int TotalPOIsCreated { get; set; }
    public int TotalListens { get; set; }
    public List<TopPOIDto> TopPOIs { get; set; } = new();
}

#endregion
