using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using ZoneGuide.API.Hubs;

namespace ZoneGuide.API.Services;

public interface IQrRealtimeMonitoringService
{
    Task<QrMonitoringSnapshotDto> RegisterAccessAsync(int poiId, string deviceId, string? ipAddress, string? userAgent, bool hasStableCookie);
    QrMonitoringSnapshotDto GetSnapshot();
}

public sealed class QrRealtimeMonitoringService : IQrRealtimeMonitoringService, IDisposable
{
    private readonly TimeSpan _activeWindow;
    private readonly TimeSpan _lastMinuteWindow;
    private readonly TimeSpan _queueRetentionWindow;

    private readonly ConcurrentDictionary<string, DateTime> _lastSeenByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _uniqueDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<DateTime> _accessTimestamps = new();
    private readonly IHubContext<QrMonitoringHub> _hubContext;
    private readonly ILogger<QrRealtimeMonitoringService> _logger;
    private readonly object _broadcastStateLock = new();
    private readonly PeriodicTimer _snapshotTimer;
    private readonly CancellationTokenSource _loopCts = new();
    private readonly Task _snapshotLoopTask;

    private long _totalAccessCount;
    private DateTime? _lastAccessAtUtc;
    private int? _lastPoiId;
    private readonly object _snapshotLock = new();
    private string _lastBroadcastSignature = string.Empty;

    public QrRealtimeMonitoringService(
        IHubContext<QrMonitoringHub> hubContext,
        ILogger<QrRealtimeMonitoringService> logger,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _logger = logger;

        var activeWindowSeconds = Clamp(configuration.GetValue<int?>("QrMonitoring:ActiveWindowSeconds") ?? 60, 10, 3600);
        var lastMinuteWindowSeconds = Clamp(configuration.GetValue<int?>("QrMonitoring:LastMinuteWindowSeconds") ?? 60, 10, 3600);
        var broadcastIntervalSeconds = Clamp(configuration.GetValue<int?>("QrMonitoring:BroadcastIntervalSeconds") ?? 1, 1, 10);

        _activeWindow = TimeSpan.FromSeconds(activeWindowSeconds);
        _lastMinuteWindow = TimeSpan.FromSeconds(lastMinuteWindowSeconds);
        _queueRetentionWindow = _activeWindow >= _lastMinuteWindow ? _activeWindow : _lastMinuteWindow;
        _snapshotTimer = new PeriodicTimer(TimeSpan.FromSeconds(broadcastIntervalSeconds));

        _snapshotLoopTask = Task.Run(() => RunSnapshotLoopAsync(_loopCts.Token));
    }

    public async Task<QrMonitoringSnapshotDto> RegisterAccessAsync(int poiId, string deviceId, string? ipAddress, string? userAgent, bool hasStableCookie)
    {
        var now = DateTime.UtcNow;
        var normalizedDeviceId = ResolveDeviceId(deviceId, ipAddress, userAgent, hasStableCookie);

        _lastSeenByDevice[normalizedDeviceId] = now;
        _uniqueDevices[normalizedDeviceId] = 1;
        _accessTimestamps.Enqueue(now);

        Interlocked.Increment(ref _totalAccessCount);
        lock (_snapshotLock)
        {
            _lastAccessAtUtc = now;
            _lastPoiId = poiId;
        }

        var snapshot = BuildSnapshot(now);

        await BroadcastSnapshotIfChangedAsync(snapshot, poiId);

        return snapshot;
    }

    public QrMonitoringSnapshotDto GetSnapshot()
    {
        return BuildSnapshot(DateTime.UtcNow);
    }

    private QrMonitoringSnapshotDto BuildSnapshot(DateTime now)
    {
        CleanupOldEntries(now);

        var activeThreshold = now - _activeWindow;
        var activeDeviceCount = _lastSeenByDevice.Values.Count(lastSeen => lastSeen >= activeThreshold);

        var minuteThreshold = now - _lastMinuteWindow;
        var accessesLastMinute = _accessTimestamps.Count(ts => ts >= minuteThreshold);

        DateTime? lastAccessAtUtc;
        int? lastPoiId;

        lock (_snapshotLock)
        {
            lastAccessAtUtc = _lastAccessAtUtc;
            lastPoiId = _lastPoiId;
        }

        return new QrMonitoringSnapshotDto
        {
            ActiveDeviceCount = activeDeviceCount,
            UniqueDeviceCount = _uniqueDevices.Count,
            TotalAccessCount = Interlocked.Read(ref _totalAccessCount),
            AccessesLastMinute = accessesLastMinute,
            LastAccessAtUtc = lastAccessAtUtc,
            LastPoiId = lastPoiId,
            ActiveWindowSeconds = (int)_activeWindow.TotalSeconds,
            LastMinuteWindowSeconds = (int)_lastMinuteWindow.TotalSeconds
        };
    }

    private void CleanupOldEntries(DateTime now)
    {
        var activeThreshold = now - _activeWindow;

        foreach (var kvp in _lastSeenByDevice)
        {
            if (kvp.Value < activeThreshold)
            {
                _lastSeenByDevice.TryRemove(kvp.Key, out _);
            }
        }

        var queueThreshold = now - _queueRetentionWindow;
        while (_accessTimestamps.TryPeek(out var ts) && ts < queueThreshold)
        {
            _accessTimestamps.TryDequeue(out _);
        }
    }

    private string ResolveDeviceId(string deviceId, string? ipAddress, string? userAgent, bool hasStableCookie)
    {
        if (hasStableCookie)
        {
            return deviceId;
        }

        var fingerprint = BuildFingerprint(ipAddress, userAgent);
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return deviceId;
        }

        return BuildDeterministicDeviceId(fingerprint);
    }

    private static string? BuildFingerprint(string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var ip = ipAddress.Trim();
        var ua = userAgent.Trim();
        if (ip.Length == 0 || ua.Length == 0)
        {
            return null;
        }

        return string.Concat(ip, "|", ua);
    }

    private static string BuildDeterministicDeviceId(string fingerprint)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return "fp-" + Convert.ToHexString(bytes);
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private async Task RunSnapshotLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _snapshotTimer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = BuildSnapshot(DateTime.UtcNow);
                await BroadcastSnapshotIfChangedAsync(snapshot, null);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task BroadcastSnapshotIfChangedAsync(QrMonitoringSnapshotDto snapshot, int? poiId)
    {
        var signature = BuildSignature(snapshot);
        lock (_broadcastStateLock)
        {
            if (string.Equals(signature, _lastBroadcastSignature, StringComparison.Ordinal))
            {
                return;
            }
        }

        try
        {
            await _hubContext.Clients.All.SendAsync("QrMonitorUpdated", snapshot);
            lock (_broadcastStateLock)
            {
                _lastBroadcastSignature = signature;
            }
        }
        catch (Exception ex)
        {
            if (poiId.HasValue)
            {
                _logger.LogWarning(ex, "Failed to broadcast QR monitoring update for POI {PoiId}", poiId.Value);
                return;
            }

            _logger.LogDebug(ex, "Failed to broadcast periodic QR monitoring update");
        }
    }

    private static string BuildSignature(QrMonitoringSnapshotDto snapshot)
    {
        return $"{snapshot.ActiveDeviceCount}|{snapshot.UniqueDeviceCount}|{snapshot.TotalAccessCount}|{snapshot.AccessesLastMinute}|{snapshot.LastPoiId}|{snapshot.LastAccessAtUtc?.Ticks}";
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
}

public sealed class QrMonitoringSnapshotDto
{
    public int ActiveDeviceCount { get; set; }
    public int UniqueDeviceCount { get; set; }
    public long TotalAccessCount { get; set; }
    public int AccessesLastMinute { get; set; }
    public DateTime? LastAccessAtUtc { get; set; }
    public int? LastPoiId { get; set; }
    public int ActiveWindowSeconds { get; set; }
    public int LastMinuteWindowSeconds { get; set; }
}
