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

    /// <summary>
    /// Tạo tài khoản mới (Admin)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email, tên hiển thị và mật khẩu là bắt buộc" });
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.User)
            return BadRequest(new { message = "Role không hợp lệ" });

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Status không hợp lệ" });

        var existingUsers = await _authService.GetAllUsersAsync();
        if (existingUsers.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Email đã tồn tại" });

        if (existingUsers.Any(u => u.DisplayName.Equals(request.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Tên hiển thị đã tồn tại" });

        var registerResult = await _authService.RegisterAsync(new RegisterDto
        {
            Email = request.Email.Trim(),
            Password = request.Password,
            DisplayName = request.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            RegisterAsContributor = role == UserRole.Contributor
        });

        if (!registerResult.Success || registerResult.User == null)
            return BadRequest(new { message = registerResult.Message ?? "Không thể tạo tài khoản" });

        var roleUpdated = await _authService.UpdateUserRoleAsync(new UpdateUserRoleDto
        {
            UserId = registerResult.User.Id,
            Role = role,
            Status = status
        });

        if (!roleUpdated)
            return BadRequest(new { message = "Không thể cập nhật role/trạng thái sau khi tạo" });

        var createdUser = await _authService.GetUserByIdAsync(registerResult.User.Id);
        return Ok(createdUser);
    }

    /// <summary>
    /// Cập nhật thông tin tài khoản (Admin)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] AdminUpdateUserRequest request)
    {
        var currentUser = await _authService.GetUserByIdAsync(id);
        if (currentUser == null)
            return NotFound(new { message = "Không tìm thấy user" });

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { message = "Email và tên hiển thị là bắt buộc" });

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.User)
            return BadRequest(new { message = "Role không hợp lệ" });

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "Status không hợp lệ" });

        var existingUsers = await _authService.GetAllUsersAsync();
        if (existingUsers.Any(u => u.Id != id && u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Email đã tồn tại" });

        if (existingUsers.Any(u => u.Id != id && u.DisplayName.Equals(request.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Tên hiển thị đã tồn tại" });

        var updatedProfile = await _authService.UpdateUserAsync(id, new UpdateUserDto
        {
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            AvatarUrl = request.AvatarUrl
        });

        if (updatedProfile == null)
            return NotFound(new { message = "Không tìm thấy user" });

        var roleUpdated = await _authService.UpdateUserRoleAsync(new UpdateUserRoleDto
        {
            UserId = id,
            Role = role,
            Status = status
        });

        if (!roleUpdated)
            return NotFound(new { message = "Không tìm thấy user" });

        var updatedUser = await _authService.GetUserByIdAsync(id);
        return Ok(updatedUser);
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

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "Contributor";
    public string Status { get; set; } = "Active";
}

public class AdminUpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Contributor";
    public string Status { get; set; } = "Active";
}
