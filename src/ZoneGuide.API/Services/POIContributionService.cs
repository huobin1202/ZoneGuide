using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

public interface IPOIContributionService
{
    // Contributor methods
    Task<POIContributionDto> CreateContributionAsync(int contributorId, CreatePOIContributionDto dto);
    Task<POIContributionDto?> UpdateContributionAsync(int contributorId, int id, UpdatePOIContributionDto dto);
    Task<bool> DeleteContributionAsync(int contributorId, int id);
    Task<bool> SubmitForReviewAsync(int contributorId, int id);
    Task<List<POIContributionDto>> GetMyContributionsAsync(int contributorId, POIApprovalStatus? status = null);
    Task<POIContributionDto?> GetContributionByIdAsync(int contributorId, int id);
    
    // Admin/Reviewer methods
    Task<List<POIContributionDto>> GetPendingContributionsAsync();
    Task<List<POIContributionDto>> GetAllContributionsAsync(POIApprovalStatus? status = null);
    Task<POIContributionDto?> ReviewContributionAsync(int reviewerId, ReviewPOIContributionDto dto);
    Task<List<POIApprovalHistoryDto>> GetApprovalHistoryAsync(int contributionId);
    
    // Statistics
    Task<ContributionStatsDto> GetContributionStatsAsync(int? contributorId = null);
}

public class POIContributionService : IPOIContributionService
{
    private readonly AppDbContext _context;
    
    public POIContributionService(AppDbContext context)
    {
        _context = context;
    }
    
    #region Contributor Methods
    
    public async Task<POIContributionDto> CreateContributionAsync(int contributorId, CreatePOIContributionDto dto)
    {
        var contribution = new POIContributionEntity
        {
            ContributorId = contributorId,
            OriginalPOIId = dto.OriginalPOIId,
            Name = dto.Name,
            ShortDescription = dto.ShortDescription ?? string.Empty,
            FullDescription = dto.FullDescription ?? string.Empty,
            Latitude = dto.Latitude ?? 0,
            Longitude = dto.Longitude ?? 0,
            TriggerRadius = dto.TriggerRadius,
            ApproachRadius = dto.ApproachRadius,
            Priority = dto.Priority,
            AudioUrl = dto.AudioUrl,
            TTSScript = dto.TTSScript,
            ImageUrl = dto.ImageUrl,
            MapLink = dto.MapLink,
            Category = dto.Category,
            Language = dto.Language,
            ContributorNotes = dto.ContributorNotes,
            Status = POIApprovalStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.POIContributions.Add(contribution);
        await _context.SaveChangesAsync();
        
        return await GetContributionDtoAsync(contribution.Id);
    }
    
    public async Task<POIContributionDto?> UpdateContributionAsync(int contributorId, int id, UpdatePOIContributionDto dto)
    {
        var contribution = await _context.POIContributions
            .FirstOrDefaultAsync(c => c.Id == id && c.ContributorId == contributorId);
        
        if (contribution == null) return null;
        
        // Only allow editing if in Draft or NeedsRevision status
        if (contribution.Status != POIApprovalStatus.Draft && 
            contribution.Status != POIApprovalStatus.NeedsRevision)
        {
            return null;
        }
        
        if (dto.Name != null) contribution.Name = dto.Name;
        if (dto.ShortDescription != null) contribution.ShortDescription = dto.ShortDescription;
        if (dto.FullDescription != null) contribution.FullDescription = dto.FullDescription;
        if (dto.Latitude.HasValue) contribution.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) contribution.Longitude = dto.Longitude.Value;
        if (dto.TriggerRadius.HasValue) contribution.TriggerRadius = dto.TriggerRadius.Value;
        if (dto.ApproachRadius.HasValue) contribution.ApproachRadius = dto.ApproachRadius.Value;
        if (dto.Priority.HasValue) contribution.Priority = dto.Priority.Value;
        if (dto.AudioUrl != null) contribution.AudioUrl = dto.AudioUrl;
        if (dto.TTSScript != null) contribution.TTSScript = dto.TTSScript;
        if (dto.ImageUrl != null) contribution.ImageUrl = dto.ImageUrl;
        if (dto.MapLink != null) contribution.MapLink = dto.MapLink;
        if (dto.Category != null) contribution.Category = dto.Category;
        if (dto.ContributorNotes != null) contribution.ContributorNotes = dto.ContributorNotes;
        
        contribution.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return await GetContributionDtoAsync(id);
    }
    
    public async Task<bool> DeleteContributionAsync(int contributorId, int id)
    {
        var contribution = await _context.POIContributions
            .FirstOrDefaultAsync(c => c.Id == id && c.ContributorId == contributorId);
        
        if (contribution == null) return false;
        
        // Only allow deleting if in Draft status
        if (contribution.Status != POIApprovalStatus.Draft)
        {
            return false;
        }
        
        _context.POIContributions.Remove(contribution);
        await _context.SaveChangesAsync();
        
        return true;
    }
    
    public async Task<bool> SubmitForReviewAsync(int contributorId, int id)
    {
        var contribution = await _context.POIContributions
            .FirstOrDefaultAsync(c => c.Id == id && c.ContributorId == contributorId);
        
        if (contribution == null) return false;
        
        // Only allow submitting if in Draft or NeedsRevision status
        if (contribution.Status != POIApprovalStatus.Draft && 
            contribution.Status != POIApprovalStatus.NeedsRevision)
        {
            return false;
        }
        
        var oldStatus = contribution.Status;
        contribution.Status = POIApprovalStatus.Pending;
        contribution.SubmittedAt = DateTime.UtcNow;
        contribution.UpdatedAt = DateTime.UtcNow;
        
        // Add history
        _context.POIApprovalHistories.Add(new POIApprovalHistoryEntity
        {
            ContributionId = id,
            OldStatus = oldStatus,
            NewStatus = POIApprovalStatus.Pending,
            Notes = "Gửi yêu cầu duyệt",
            ActionById = contributorId,
            CreatedAt = DateTime.UtcNow
        });
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<List<POIContributionDto>> GetMyContributionsAsync(int contributorId, POIApprovalStatus? status = null)
    {
        var query = _context.POIContributions
            .Include(c => c.Contributor)
            .Include(c => c.Reviewer)
            .Where(c => c.ContributorId == contributorId);
        
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }
        
        var contributions = await query
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
        
        return contributions.Select(MapToDto).ToList();
    }
    
    public async Task<POIContributionDto?> GetContributionByIdAsync(int contributorId, int id)
    {
        var contribution = await _context.POIContributions
            .Include(c => c.Contributor)
            .Include(c => c.Reviewer)
            .FirstOrDefaultAsync(c => c.Id == id && c.ContributorId == contributorId);
        
        return contribution == null ? null : MapToDto(contribution);
    }
    
    #endregion
    
    #region Admin Methods
    
    public async Task<List<POIContributionDto>> GetPendingContributionsAsync()
    {
        var contributions = await _context.POIContributions
            .Include(c => c.Contributor)
            .Include(c => c.Reviewer)
            .Where(c => c.Status == POIApprovalStatus.Pending)
            .OrderBy(c => c.SubmittedAt)
            .ToListAsync();
        
        return contributions.Select(MapToDto).ToList();
    }
    
    public async Task<List<POIContributionDto>> GetAllContributionsAsync(POIApprovalStatus? status = null)
    {
        var query = _context.POIContributions
            .Include(c => c.Contributor)
            .Include(c => c.Reviewer)
            .AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }
        
        var contributions = await query
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
        
        return contributions.Select(MapToDto).ToList();
    }
    
    public async Task<POIContributionDto?> ReviewContributionAsync(int reviewerId, ReviewPOIContributionDto dto)
    {
        var contribution = await _context.POIContributions
            .Include(c => c.Contributor)
            .FirstOrDefaultAsync(c => c.Id == dto.ContributionId);
        
        if (contribution == null) return null;
        
        // Only allow reviewing if in Pending status
        if (contribution.Status != POIApprovalStatus.Pending)
        {
            return null;
        }
        
        var oldStatus = contribution.Status;
        contribution.Status = dto.Decision;
        contribution.ReviewerId = reviewerId;
        contribution.ReviewerFeedback = dto.Feedback;
        contribution.ReviewedAt = DateTime.UtcNow;
        contribution.UpdatedAt = DateTime.UtcNow;
        
        // Add history
        _context.POIApprovalHistories.Add(new POIApprovalHistoryEntity
        {
            ContributionId = dto.ContributionId,
            OldStatus = oldStatus,
            NewStatus = dto.Decision,
            Notes = dto.Feedback,
            ActionById = reviewerId,
            CreatedAt = DateTime.UtcNow
        });
        
        // If approved, update original POI (edit flow) or create new POI (new contribution flow)
        if (dto.Decision == POIApprovalStatus.Approved)
        {
            POIEntity? poi = null;

            if (contribution.OriginalPOIId.HasValue)
            {
                poi = await _context.POIs.FirstOrDefaultAsync(p => p.Id == contribution.OriginalPOIId.Value);
            }

            if (poi != null)
            {
                poi.Name = contribution.Name;
                poi.ShortDescription = contribution.ShortDescription;
                poi.FullDescription = contribution.FullDescription;
                poi.Latitude = contribution.Latitude;
                poi.Longitude = contribution.Longitude;
                poi.TriggerRadius = contribution.TriggerRadius;
                poi.ApproachRadius = contribution.ApproachRadius;
                poi.Priority = contribution.Priority;
                poi.AudioUrl = contribution.AudioUrl;
                poi.TTSScript = contribution.TTSScript;
                poi.ImageUrl = contribution.ImageUrl;
                poi.MapLink = contribution.MapLink;
                poi.Category = contribution.Category;
                poi.Language = contribution.Language;
                poi.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                poi = new POIEntity
                {
                    UniqueCode = $"POI-{DateTime.UtcNow:yyyyMMddHHmmss}-{contribution.Id}",
                    Name = contribution.Name,
                    ShortDescription = contribution.ShortDescription,
                    FullDescription = contribution.FullDescription,
                    Latitude = contribution.Latitude,
                    Longitude = contribution.Longitude,
                    TriggerRadius = contribution.TriggerRadius,
                    ApproachRadius = contribution.ApproachRadius,
                    Priority = contribution.Priority,
                    AudioUrl = contribution.AudioUrl,
                    TTSScript = contribution.TTSScript,
                    ImageUrl = contribution.ImageUrl,
                    MapLink = contribution.MapLink,
                    Category = contribution.Category,
                    Language = contribution.Language,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.POIs.Add(poi);
            }

            await _context.SaveChangesAsync();

            // Persist link so subsequent contributor edits can target the same POI.
            contribution.OriginalPOIId = poi.Id;
            contribution.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        
        return await GetContributionDtoAsync(dto.ContributionId);
    }
    
    public async Task<List<POIApprovalHistoryDto>> GetApprovalHistoryAsync(int contributionId)
    {
        var histories = await _context.POIApprovalHistories
            .Include(h => h.ActionBy)
            .Where(h => h.ContributionId == contributionId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        
        return histories.Select(h => new POIApprovalHistoryDto
        {
            Id = h.Id,
            ContributionId = h.ContributionId,
            OldStatus = h.OldStatus.ToString(),
            NewStatus = h.NewStatus.ToString(),
            Notes = h.Notes,
            ActionById = h.ActionById,
            ActionByName = h.ActionBy?.DisplayName ?? "Unknown",
            CreatedAt = h.CreatedAt
        }).ToList();
    }
    
    #endregion
    
    #region Statistics
    
    public async Task<ContributionStatsDto> GetContributionStatsAsync(int? contributorId = null)
    {
        var query = _context.POIContributions.AsQueryable();
        
        if (contributorId.HasValue)
        {
            query = query.Where(c => c.ContributorId == contributorId.Value);
        }
        
        var stats = await query
            .GroupBy(c => 1)
            .Select(g => new ContributionStatsDto
            {
                TotalContributions = g.Count(),
                DraftCount = g.Count(c => c.Status == POIApprovalStatus.Draft),
                PendingCount = g.Count(c => c.Status == POIApprovalStatus.Pending),
                ApprovedCount = g.Count(c => c.Status == POIApprovalStatus.Approved),
                RejectedCount = g.Count(c => c.Status == POIApprovalStatus.Rejected),
                NeedsRevisionCount = g.Count(c => c.Status == POIApprovalStatus.NeedsRevision)
            })
            .FirstOrDefaultAsync();
        
        return stats ?? new ContributionStatsDto();
    }
    
    #endregion
    
    #region Private Methods
    
    private async Task<POIContributionDto> GetContributionDtoAsync(int id)
    {
        var contribution = await _context.POIContributions
            .Include(c => c.Contributor)
            .Include(c => c.Reviewer)
            .FirstAsync(c => c.Id == id);
        
        return MapToDto(contribution);
    }
    
    private POIContributionDto MapToDto(POIContributionEntity entity) => new()
    {
        Id = entity.Id,
        OriginalPOIId = entity.OriginalPOIId,
        ContributorId = entity.ContributorId,
        ContributorName = entity.Contributor?.DisplayName ?? "Unknown",
        Name = entity.Name,
        ShortDescription = entity.ShortDescription,
        FullDescription = entity.FullDescription,
        Latitude = entity.Latitude,
        Longitude = entity.Longitude,
        TriggerRadius = entity.TriggerRadius,
        ApproachRadius = entity.ApproachRadius,
        Priority = entity.Priority,
        AudioUrl = entity.AudioUrl,
        TTSScript = entity.TTSScript,
        ImageUrl = entity.ImageUrl,
        MapLink = entity.MapLink,
        Category = entity.Category,
        Language = entity.Language,
        Status = entity.Status.ToString(),
        ContributorNotes = entity.ContributorNotes,
        ReviewerFeedback = entity.ReviewerFeedback,
        ReviewerId = entity.ReviewerId,
        ReviewerName = entity.Reviewer?.DisplayName,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        SubmittedAt = entity.SubmittedAt,
        ReviewedAt = entity.ReviewedAt
    };
    
    #endregion
}
