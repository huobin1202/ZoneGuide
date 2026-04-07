using ZoneGuide.Shared.Models;
using SQLite;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service quản lý SQLite Database
/// </summary>
public class DatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public DatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "ZoneGuide.db");
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_database != null)
            return _database;

        _database = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        // Tạo các bảng
        await _database.CreateTableAsync<POI>();
        await _database.CreateTableAsync<POITranslation>();
        await _database.CreateTableAsync<Tour>();
        await _database.CreateTableAsync<TourTranslation>();
        await _database.CreateTableAsync<LocationHistory>();
        await _database.CreateTableAsync<NarrationHistory>();
        await _database.CreateTableAsync<POIStatistics>();

        return _database;
    }

    public async Task<int> ClearAllDataAsync()
    {
        var db = await GetConnectionAsync();
        var count = 0;
        count += await db.DeleteAllAsync<POI>();
        count += await db.DeleteAllAsync<POITranslation>();
        count += await db.DeleteAllAsync<Tour>();
        count += await db.DeleteAllAsync<TourTranslation>();
        return count;
    }

    public async Task<int> ClearAnalyticsDataAsync()
    {
        var db = await GetConnectionAsync();
        var count = 0;
        count += await db.DeleteAllAsync<LocationHistory>();
        count += await db.DeleteAllAsync<NarrationHistory>();
        count += await db.DeleteAllAsync<POIStatistics>();
        return count;
    }
}
