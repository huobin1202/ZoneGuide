using System.Text.Json;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Services;

public interface IUserSessionService
{
    Task<bool> IsAuthenticatedAsync();
    Task<AuthResponseDto> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<string?> GetAccessTokenAsync();
}

public class UserSessionService : IUserSessionService
{
    private const string AccessTokenKey = "auth_access_token";
    private const string RefreshTokenKey = "auth_refresh_token";
    private const string ExpiresAtKey = "auth_expires_at_ticks";
    private const string UserInfoKey = "auth_user_info";

    private readonly ApiService _apiService;

    public UserSessionService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var ticks = Preferences.Get(ExpiresAtKey, 0L);
        if (ticks <= 0)
            return true;

        var expiresAtUtc = new DateTime(ticks, DateTimeKind.Utc);
        return expiresAtUtc > DateTime.UtcNow.AddMinutes(1);
    }

    public async Task<AuthResponseDto> LoginAsync(string email, string password)
    {
        var response = await _apiService.LoginAsync(new LoginDto
        {
            Email = email.Trim(),
            Password = password
        });

        if (response.Success && !string.IsNullOrWhiteSpace(response.AccessToken))
        {
            await SaveSessionAsync(response);
        }

        return response;
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        SecureStorage.Remove(UserInfoKey);
        Preferences.Remove(ExpiresAtKey);
        await Task.CompletedTask;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.GetAsync(AccessTokenKey);
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveSessionAsync(AuthResponseDto response)
    {
        await SecureStorage.SetAsync(AccessTokenKey, response.AccessToken ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
        {
            await SecureStorage.SetAsync(RefreshTokenKey, response.RefreshToken);
        }
        else
        {
            SecureStorage.Remove(RefreshTokenKey);
        }

        if (response.User != null)
        {
            await SecureStorage.SetAsync(UserInfoKey, JsonSerializer.Serialize(response.User));
        }
        else
        {
            SecureStorage.Remove(UserInfoKey);
        }

        if (response.ExpiresAt.HasValue)
        {
            Preferences.Set(ExpiresAtKey, response.ExpiresAt.Value.ToUniversalTime().Ticks);
        }
        else
        {
            Preferences.Remove(ExpiresAtKey);
        }
    }
}
