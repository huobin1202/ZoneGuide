using System.Security.Claims;
using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContributionsController : ControllerBase
{
    private readonly IPOIContributionService _contributionService;
    
    public ContributionsController(IPOIContributionService contributionService)
    {
        _contributionService = contributionService;
    }
    
    #region Contributor Endpoints
    
    /// <summary>
    /// Tạo đóng góp POI mới (Contributor)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult<POIContributionDto>> CreateContribution([FromBody] CreatePOIContributionDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var contribution = await _contributionService.CreateContributionAsync(userId.Value, dto);
        return CreatedAtAction(nameof(GetMyContribution), new { id = contribution.Id }, contribution);
    }
    
    /// <summary>
    /// Cập nhật đóng góp (chỉ khi Draft hoặc NeedsRevision)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult<POIContributionDto>> UpdateContribution(int id, [FromBody] UpdatePOIContributionDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var contribution = await _contributionService.UpdateContributionAsync(userId.Value, id, dto);
        if (contribution == null)
            return NotFound(new { message = "Không tìm thấy hoặc không thể chỉnh sửa đóng góp này" });
        
        return Ok(contribution);
    }
    
    /// <summary>
    /// Xóa đóng góp (chỉ khi Draft)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult> DeleteContribution(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var result = await _contributionService.DeleteContributionAsync(userId.Value, id);
        if (!result)
            return NotFound(new { message = "Không tìm thấy hoặc không thể xóa đóng góp này" });
        
        return NoContent();
    }
    
    /// <summary>
    /// Gửi đóng góp để duyệt
    /// </summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult> SubmitForReview(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var result = await _contributionService.SubmitForReviewAsync(userId.Value, id);
        if (!result)
            return BadRequest(new { message = "Không thể gửi duyệt đóng góp này" });
        
        return Ok(new { message = "Đã gửi yêu cầu duyệt" });
    }
    
    /// <summary>
    /// Lấy danh sách đóng góp của tôi
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult<List<POIContributionDto>>> GetMyContributions([FromQuery] string? status = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        POIApprovalStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<POIApprovalStatus>(status, true, out var s))
        {
            statusEnum = s;
        }
        
        var contributions = await _contributionService.GetMyContributionsAsync(userId.Value, statusEnum);
        return Ok(contributions);
    }
    
    /// <summary>
    /// Lấy chi tiết một đóng góp của tôi
    /// </summary>
    [HttpGet("my/{id}")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult<POIContributionDto>> GetMyContribution(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var contribution = await _contributionService.GetContributionByIdAsync(userId.Value, id);
        if (contribution == null)
            return NotFound();
        
        return Ok(contribution);
    }
    
    /// <summary>
    /// Lấy thống kê đóng góp của tôi
    /// </summary>
    [HttpGet("my/stats")]
    [Authorize(Roles = "Contributor,Admin")]
    public async Task<ActionResult<ContributionStatsDto>> GetMyStats()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var stats = await _contributionService.GetContributionStatsAsync(userId.Value);
        return Ok(stats);
    }
    
    #endregion
    
    #region Admin Endpoints
    
    /// <summary>
    /// Lấy danh sách đóng góp chờ duyệt (Admin)
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<POIContributionDto>>> GetPendingContributions()
    {
        var contributions = await _contributionService.GetPendingContributionsAsync();
        return Ok(contributions);
    }
    
    /// <summary>
    /// Lấy tất cả đóng góp (Admin)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<POIContributionDto>>> GetAllContributions([FromQuery] string? status = null)
    {
        POIApprovalStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<POIApprovalStatus>(status, true, out var s))
        {
            statusEnum = s;
        }
        
        var contributions = await _contributionService.GetAllContributionsAsync(statusEnum);
        return Ok(contributions);
    }
    
    /// <summary>
    /// Duyệt/từ chối đóng góp (Admin)
    /// </summary>
    [HttpPost("review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<POIContributionDto>> ReviewContribution([FromBody] ReviewPOIContributionDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var contribution = await _contributionService.ReviewContributionAsync(userId.Value, dto);
        if (contribution == null)
            return NotFound(new { message = "Không tìm thấy hoặc không thể duyệt đóng góp này" });
        
        return Ok(contribution);
    }
    
    /// <summary>
    /// Lấy lịch sử duyệt của một đóng góp (Admin)
    /// </summary>
    [HttpGet("{id}/history")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<POIApprovalHistoryDto>>> GetApprovalHistory(int id)
    {
        var history = await _contributionService.GetApprovalHistoryAsync(id);
        return Ok(history);
    }
    
    /// <summary>
    /// Lấy thống kê tổng thể (Admin)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ContributionStatsDto>> GetAllStats()
    {
        var stats = await _contributionService.GetContributionStatsAsync();
        return Ok(stats);
    }
    
    #endregion
    
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
