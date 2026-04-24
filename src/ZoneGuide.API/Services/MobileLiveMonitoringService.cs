using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ZoneGuide.API.Hubs;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.API.Services;

public interface IMobileLiveMonitoringService
{
    Task<MobileLiveMonitoringSnapshotDto> RegisterHeartbeatAsync(
        MobileLiveHeartbeatDto heartbeat,
        int? userId,
        string? userDisplayName,
        string? userEmail);
    Task<MobileLiveMonitoringSnapshotDto> UnregisterSessionAsync(string sessionId);

    MobileLiveMonitoringSnapshotDto GetSnapshot();
}

public sealed class MobileLiveMonitoringService : IMobileLiveMonitoringService, IDisposable
{
    private readonly ConcurrentDictionary<string, MobileLiveSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHubContext<MobileMonitoringHub> _hubContext;
    private readonly ILogger<MobileLiveMonitoringService> _logger;
    private readonly TimeSpan _activeWindow;
    private readonly PeriodicTimer _snapshotTimer;
    private readonly CancellationTokenSource _loopCts = new();
    private readonly Task _snapshotLoopTask;
    private readonly object _broadcastLock = new();
    private string _lastBroadcastSignature = string.Empty;
    private DateTime? _lastUpdatedAtUtc;

    public MobileLiveMonitoringService(
        IHubContext<MobileMonitoringHub> hubContext,
        ILogger<MobileLiveMonitoringService> logger,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _logger = logger;

        var activeWindowSeconds = Clamp(configuration.GetValue<int?>("MobileMonitoring:ActiveWindowSeconds") ?? 10, 1, 600);
        var broadcastIntervalSeconds = Clamp(configuration.GetValue<int?>("MobileMonitoring:BroadcastIntervalSeconds") ?? 2, 1, 10);

        _activeWindow = TimeSpan.FromSeconds(activeWindowSeconds);
        _snapshotTimer = new PeriodicTimer(TimeSpan.FromSeconds(broadcastIntervalSeconds));
        _snapshotLoopTask = Task.Run(() => RunSnapshotLoopAsync(_loopCts.Token));
    }

    public async Task<MobileLiveMonitoringSnapshotDto> RegisterHeartbeatAsync(
        MobileLiveHeartbeatDto heartbeat,
        int? userId,
        string? userDisplayName,
        string? userEmail)
    {
        var now = DateTime.UtcNow;
        var sessionId = string.IsNullOrWhiteSpace(heartbeat.SessionId)
            ? Guid.NewGuid().ToString("N")
            : heartbeat.SessionId.Trim();

        var deviceId = string.IsNullOrWhiteSpace(heartbeat.DeviceId)
            ? "unknown-device"
            : heartbeat.DeviceId.Trim();
//dang ky cap nhat phien dang va gui cap nhat cho cac client dang lang nghe
        _sessions.AddOrUpdate(
            sessionId,
            _ => MobileLiveSessionState.FromHeartbeat(heartbeat, sessionId, deviceId, now, userId, userDisplayName, userEmail),
            (_, existing) =>
            {
                existing.ApplyHeartbeat(heartbeat, deviceId, now, userId, userDisplayName, userEmail);
                return existing;
            });

        _lastUpdatedAtUtc = now;

        var snapshot = BuildSnapshot(now);
        await BroadcastSnapshotIfChangedAsync(snapshot);
        return snapshot;
    }

    public MobileLiveMonitoringSnapshotDto GetSnapshot()
    {
        return BuildSnapshot(DateTime.UtcNow);
    }

    public async Task<MobileLiveMonitoringSnapshotDto> UnregisterSessionAsync(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            //huy dang ky phien 
            _sessions.TryRemove(sessionId.Trim(), out _);
        }

        var snapshot = BuildSnapshot(DateTime.UtcNow);
        await BroadcastSnapshotIfChangedAsync(snapshot);
        return snapshot;
    }

    private MobileLiveMonitoringSnapshotDto BuildSnapshot(DateTime now)
    {
        CleanupExpiredSessions(now);

        var sessions = _sessions.Values
            .Select(x => x.ToDto())
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToList();

        return new MobileLiveMonitoringSnapshotDto
        {
            ActiveSessionCount = sessions.Count,
            AuthenticatedSessionCount = sessions.Count(x => x.IsAuthenticated),
            AnonymousSessionCount = sessions.Count(x => !x.IsAuthenticated),
            TrackingSessionCount = sessions.Count(x => x.IsTracking),
            LastUpdatedAtUtc = _lastUpdatedAtUtc,
            ActiveWindowSeconds = (int)_activeWindow.TotalSeconds,
            Sessions = sessions
        };
    }

    private void CleanupExpiredSessions(DateTime now)
    {
        var threshold = now - _activeWindow;
        foreach (var entry in _sessions)
        {
            if (entry.Value.LastSeenAtUtc < threshold)
            {
                _sessions.TryRemove(entry.Key, out _);
            }
        }
    }

    private async Task RunSnapshotLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _snapshotTimer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = BuildSnapshot(DateTime.UtcNow);
                await BroadcastSnapshotIfChangedAsync(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task BroadcastSnapshotIfChangedAsync(MobileLiveMonitoringSnapshotDto snapshot)
    {
        var signature = BuildSignature(snapshot);
        lock (_broadcastLock)
        {
            if (string.Equals(signature, _lastBroadcastSignature, StringComparison.Ordinal))
            {
                return;
            }
        }
// đẩy trạng thái hiện tại/ mới nếu có thay đổi
        try
        {
            await _hubContext.Clients.All.SendAsync("MobileMonitorUpdated", snapshot);
            lock (_broadcastLock)
            {
                _lastBroadcastSignature = signature;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast mobile monitoring snapshot");
        }
    }

    private static string BuildSignature(MobileLiveMonitoringSnapshotDto snapshot)
    {
        var sessions = string.Join(';', snapshot.Sessions.Select(s =>
            $"{s.SessionId}:{s.LastSeenAtUtc.Ticks}:{s.HasLocationFix}:{s.Latitude:F5}:{s.Longitude:F5}:{s.IsTracking}:{s.UserId}:{s.NearestPoiId}"));

        return $"{snapshot.ActiveSessionCount}|{snapshot.AuthenticatedSessionCount}|{snapshot.TrackingSessionCount}|{snapshot.LastUpdatedAtUtc?.Ticks}|{sessions}";
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    public void Dispose()
    {
        _loopCts.Cancel();
        _snapshotTimer.Dispose();
        _loopCts.Dispose();

        try
        {
            _snapshotLoopTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private sealed class MobileLiveSessionState
    {
        public string SessionId { get; private init; } = string.Empty;
        public string DeviceId { get; private set; } = string.Empty;
        public bool IsTracking { get; private set; }
        public bool HasLocationFix { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public int? UserId { get; private set; }
        public string? UserDisplayName { get; private set; }
        public string? UserEmail { get; private set; }
        public string Platform { get; private set; } = string.Empty;
        public string? AppVersion { get; private set; }
        public string? PreferredLanguage { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double Accuracy { get; private set; }
        public double? Speed { get; private set; }
        public double? Heading { get; private set; }
        public double? Altitude { get; private set; }
        public DateTime LocationTimestampUtc { get; private set; }
        public DateTime LastSeenAtUtc { get; set; }
        public int? NearestPoiId { get; private set; }
        public string? NearestPoiName { get; private set; }
        public string? StatusMessage { get; private set; }

        public static MobileLiveSessionState FromHeartbeat(
            MobileLiveHeartbeatDto heartbeat,
            string sessionId,
            string deviceId,
            DateTime now,
            int? userId,
            string? userDisplayName,
            string? userEmail)
        {
            var state = new MobileLiveSessionState
            {
                SessionId = sessionId
            };

            state.ApplyHeartbeat(heartbeat, deviceId, now, userId, userDisplayName, userEmail);
            return state;
        }

        public void ApplyHeartbeat(
            MobileLiveHeartbeatDto heartbeat,
            string deviceId,
            DateTime now,
            int? userId,
            string? userDisplayName,
            string? userEmail)
        {
            DeviceId = deviceId;
            IsTracking = heartbeat.IsTracking;
            HasLocationFix = heartbeat.HasLocationFix;
            IsAuthenticated = userId.HasValue;
            UserId = userId;
            UserDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? heartbeat.DeviceId : userDisplayName;
            UserEmail = userEmail;
            Platform = string.IsNullOrWhiteSpace(heartbeat.Platform) ? "unknown" : heartbeat.Platform.Trim();
            AppVersion = heartbeat.AppVersion?.Trim();
            PreferredLanguage = heartbeat.PreferredLanguage?.Trim();
            if (HasLocationFix)
            {
                Latitude = heartbeat.Latitude;
                Longitude = heartbeat.Longitude;
                Accuracy = heartbeat.Accuracy;
                Speed = heartbeat.Speed;
                Heading = heartbeat.Heading;
                Altitude = heartbeat.Altitude;
                LocationTimestampUtc = heartbeat.Timestamp == default ? now : heartbeat.Timestamp.ToUniversalTime();
            }
            LastSeenAtUtc = now;
            NearestPoiId = heartbeat.NearestPoiId;
            NearestPoiName = heartbeat.NearestPoiName?.Trim();
            StatusMessage = heartbeat.StatusMessage?.Trim();
        }

        public MobileLiveSessionDto ToDto()
        {
            return new MobileLiveSessionDto
            {
                SessionId = SessionId,
                DeviceId = DeviceId,
                IsTracking = IsTracking,
                HasLocationFix = HasLocationFix,
                IsAuthenticated = IsAuthenticated,
                UserId = UserId,
                UserDisplayName = UserDisplayName,
                UserEmail = UserEmail,
                Platform = Platform,
                AppVersion = AppVersion,
                PreferredLanguage = PreferredLanguage,
                Latitude = Latitude,
                Longitude = Longitude,
                Accuracy = Accuracy,
                Speed = Speed,
                Heading = Heading,
                Altitude = Altitude,
                LocationTimestampUtc = LocationTimestampUtc,
                LastSeenAtUtc = LastSeenAtUtc,
                NearestPoiId = NearestPoiId,
                NearestPoiName = NearestPoiName,
                StatusMessage = StatusMessage
            };
        }
    }
}
