using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ZoneGuide.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(int userId);
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<UserDto?> UpdateUserAsync(int userId, UpdateUserDto dto);
    Task<bool> UpdateUserRoleAsync(UpdateUserRoleDto dto);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    string? ValidateToken(string token);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    
    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return new AuthResponseDto 
            { 
                Success = false, 
                Message = "Email đã được sử dụng" 
            };
        }
        
        // Create password hash
        CreatePasswordHash(dto.Password, out string passwordHash, out string passwordSalt);
        
        var user = new UserEntity
        {
            Email = dto.Email,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            DisplayName = dto.DisplayName,
            PhoneNumber = dto.PhoneNumber,
            Role = dto.RegisterAsContributor ? UserRole.Contributor : UserRole.User,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Generate tokens
        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();
        
        return new AuthResponseDto
        {
            Success = true,
            Message = "Đăng ký thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapToDto(user)
        };
    }
    
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        
        if (user == null)
        {
            return new AuthResponseDto 
            { 
                Success = false, 
                Message = "Email hoặc mật khẩu không đúng" 
            };
        }
        
        if (user.Status != UserStatus.Active)
        {
            return new AuthResponseDto 
            { 
                Success = false, 
                Message = "Tài khoản đã bị khóa hoặc chưa được kích hoạt" 
            };
        }
        
        if (!VerifyPasswordHash(dto.Password, user.PasswordHash, user.PasswordSalt))
        {
            return new AuthResponseDto 
            { 
                Success = false, 
                Message = "Email hoặc mật khẩu không đúng" 
            };
        }
        
        // Generate tokens
        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return new AuthResponseDto
        {
            Success = true,
            Message = "Đăng nhập thành công",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapToDto(user)
        };
    }
    
    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && 
                                      u.RefreshTokenExpiry > DateTime.UtcNow);
        
        if (user == null)
        {
            return new AuthResponseDto 
            { 
                Success = false, 
                Message = "Token không hợp lệ hoặc đã hết hạn" 
            };
        }
        
        var (accessToken, expiresAt) = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();
        
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();
        
        return new AuthResponseDto
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt,
            User = MapToDto(user)
        };
    }
    
    public async Task<bool> RevokeTokenAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;
        
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _context.SaveChangesAsync();
        
        return true;
    }
    
    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user == null ? null : MapToDto(user);
    }
    
    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Where(u => u.Status != UserStatus.Deleted)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
        
        return users.Select(MapToDto).ToList();
    }
    
    public async Task<UserDto?> UpdateUserAsync(int userId, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;
        
        if (!string.IsNullOrEmpty(dto.DisplayName))
            user.DisplayName = dto.DisplayName;
        if (dto.PhoneNumber != null)
            user.PhoneNumber = dto.PhoneNumber;
        if (dto.AvatarUrl != null)
            user.AvatarUrl = dto.AvatarUrl;
        
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return MapToDto(user);
    }
    
    public async Task<bool> UpdateUserRoleAsync(UpdateUserRoleDto dto)
    {
        var user = await _context.Users.FindAsync(dto.UserId);
        if (user == null) return false;
        
        user.Role = dto.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return true;
    }
    
    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;
        
        if (!VerifyPasswordHash(dto.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return false;
        
        CreatePasswordHash(dto.NewPassword, out string newHash, out string newSalt);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return true;
    }
    
    public string? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "ZoneGuide_Secret_Key_2024_Very_Long_Key_For_Security");
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);
            
            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
        }
        catch
        {
            return null;
        }
    }
    
    #region Private Methods
    
    private void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        using var hmac = new HMACSHA512();
        passwordSalt = Convert.ToBase64String(hmac.Key);
        passwordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }
    
    private bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        using var hmac = new HMACSHA512(saltBytes);
        var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computedHash == storedHash;
    }
    
    private (string token, DateTime expiresAt) GenerateAccessToken(UserEntity user)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "ZoneGuide_Secret_Key_2024_Very_Long_Key_For_Security");
        var expiresAt = DateTime.UtcNow.AddHours(24);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return (tokenHandler.WriteToken(token), expiresAt);
    }
    
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
    
    private UserDto MapToDto(UserEntity user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        PhoneNumber = user.PhoneNumber,
        AvatarUrl = user.AvatarUrl,
        Role = user.Role.ToString(),
        Status = user.Status.ToString(),
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
    
    #endregion
}
