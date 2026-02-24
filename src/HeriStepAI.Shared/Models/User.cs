using SQLite;

namespace HeriStepAI.Shared.Models;

/// <summary>
/// Vai trò người dùng
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Người dùng thường - chỉ xem và sử dụng app
    /// </summary>
    User = 0,
    
    /// <summary>
    /// Người đóng góp - có thể thêm POI, chờ duyệt
    /// </summary>
    Contributor = 1,
    
    /// <summary>
    /// Quản trị viên - toàn quyền
    /// </summary>
    Admin = 2
}

/// <summary>
/// Trạng thái tài khoản
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Đang chờ kích hoạt
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đang hoạt động
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Bị khóa
    /// </summary>
    Suspended = 2,
    
    /// <summary>
    /// Đã xóa
    /// </summary>
    Deleted = 3
}

/// <summary>
/// Người dùng hệ thống
/// </summary>
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Email đăng nhập (unique)
    /// </summary>
    [Indexed(Unique = true)]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Mật khẩu đã hash
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Salt cho password
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;
    
    /// <summary>
    /// Tên hiển thị
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Số điện thoại
    /// </summary>
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Avatar URL
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// Vai trò
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;
    
    /// <summary>
    /// Trạng thái tài khoản
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Active;
    
    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Thời gian cập nhật
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Lần đăng nhập cuối
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// Token refresh
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// Thời hạn refresh token
    /// </summary>
    public DateTime? RefreshTokenExpiry { get; set; }
}

/// <summary>
/// DTO đăng ký
/// </summary>
public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// true = đăng ký làm Contributor, false = User thường
    /// </summary>
    public bool RegisterAsContributor { get; set; } = false;
}

/// <summary>
/// DTO đăng nhập
/// </summary>
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO phản hồi đăng nhập
/// </summary>
public class AuthResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
}

/// <summary>
/// DTO thông tin người dùng
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "User";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// DTO cập nhật người dùng
/// </summary>
public class UpdateUserDto
{
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// DTO đổi mật khẩu
/// </summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO cập nhật role (Admin only)
/// </summary>
public class UpdateUserRoleDto
{
    public int UserId { get; set; }
    public UserRole Role { get; set; }
}
