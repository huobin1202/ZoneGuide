using System.Security.Claims;
using HeriStepAI.API.Services;
using HeriStepAI.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeriStepAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    
    /// <summary>
    /// Đăng ký tài khoản mới
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
    
    /// <summary>
    /// Đăng nhập
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (!result.Success)
            return Unauthorized(result);
        return Ok(result);
    }
    
    /// <summary>
    /// Làm mới token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        if (!result.Success)
            return Unauthorized(result);
        return Ok(result);
    }
    
    /// <summary>
    /// Đăng xuất (thu hồi token)
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        await _authService.RevokeTokenAsync(userId.Value);
        return Ok(new { message = "Đăng xuất thành công" });
    }
    
    /// <summary>
    /// Lấy thông tin user hiện tại
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var user = await _authService.GetUserByIdAsync(userId.Value);
        if (user == null) return NotFound();
        
        return Ok(user);
    }
    
    /// <summary>
    /// Cập nhật thông tin user
    /// </summary>
    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateCurrentUser([FromBody] UpdateUserDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var user = await _authService.UpdateUserAsync(userId.Value, dto);
        if (user == null) return NotFound();
        
        return Ok(user);
    }
    
    /// <summary>
    /// Đổi mật khẩu
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var result = await _authService.ChangePasswordAsync(userId.Value, dto);
        if (!result)
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng" });
        
        return Ok(new { message = "Đổi mật khẩu thành công" });
    }
    
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
