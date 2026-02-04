using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using System.Text.Json;

namespace HeriStepAI.Mobile.Services;

/// <summary>
/// Service quản lý cài đặt ứng dụng
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsKey = "app_settings";
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public async Task LoadAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(SettingsKey);
            if (!string.IsNullOrEmpty(json))
            {
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings);
            await SecureStorage.SetAsync(SettingsKey, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
        }
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await SecureStorage.GetAsync(key);
            if (!string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<T>(json);
            }
        }
        catch
        {
            // Ignore
        }
        return default;
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await SecureStorage.SetAsync(key, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings set error: {ex.Message}");
        }
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Remove(key);
        return Task.CompletedTask;
    }
}
