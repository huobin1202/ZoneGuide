using System.Security.Claims;
using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }
    
    /// <summary>
    /// Lấy danh sách tất cả users (Admin)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }
    
    /// <summary>
    /// Lấy thông tin một user (Admin)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();
        return Ok(user);
    }
    
    /// <summary>
    /// Cập nhật role của user (Admin)
    /// </summary>
    [HttpPut("{id}/role")]
    public async Task<ActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
            return BadRequest(new { message = "Không thể thay đổi role của chính mình" });
        
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest(new { message = "Role không hợp lệ" });
            
        if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Status không hợp lệ" });
        
        var result = await _authService.UpdateUserRoleAsync(new UpdateUserRoleDto
        {
            UserId = id,
            Role = role,
            Status = status
        });
        
        if (!result)
            return NotFound(new { message = "Không tìm thấy user" });
        
        return Ok(new { message = "Cập nhật tài khoản thành công" });
    }
    
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
