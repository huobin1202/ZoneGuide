using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.Concurrent;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service quản lý Geofence - Kích hoạt điểm thuyết minh
/// </summary>
public class GeofenceService : IGeofenceService
{
    private const int MinEffectiveCooldownSeconds = 1;
    private const int MaxEffectiveCooldownSeconds = 3600;

    public event EventHandler<GeofenceEvent>? GeofenceTriggered;

    private readonly List<POI> _monitoredPOIs = new();
    private readonly ConcurrentDictionary<int, DateTime> _cooldowns = new();
    private readonly ConcurrentDictionary<int, GeofenceState> _poiStates = new();
    private readonly object _lock = new();

    // Debounce settings
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(700);
    private readonly ConcurrentDictionary<int, DateTime> _lastTriggerTime = new();

    public IReadOnlyList<POI> MonitoredPOIs => _monitoredPOIs.AsReadOnly();
    public POI? NearestPOI { get; private set; }
    public double? NearestPOIDistance { get; private set; }
    public bool IsInitialized { get; private set; }

    public void AddPOI(POI poi)
    {
        lock (_lock)
        {
            if (!_monitoredPOIs.Any(p => p.Id == poi.Id))
            {
                _monitoredPOIs.Add(poi);
                _poiStates[poi.Id] = new GeofenceState();
            }
        }
    }

    public void AddPOIs(IEnumerable<POI> pois)
    {
        foreach (var poi in pois)
        {
            AddPOI(poi);
        }
    }

    public void RemovePOI(int poiId)
    {
        lock (_lock)
        {
            var poi = _monitoredPOIs.FirstOrDefault(p => p.Id == poiId);
            if (poi != null)
            {
                _monitoredPOIs.Remove(poi);
                _poiStates.TryRemove(poiId, out _);
                _cooldowns.TryRemove(poiId, out _);
                _lastTriggerTime.TryRemove(poiId, out _);
            }
        }
    }

    public void ClearPOIs()
    {
        lock (_lock)
        {
            _monitoredPOIs.Clear();
            _poiStates.Clear();
            _cooldowns.Clear();
            _lastTriggerTime.Clear();
        }
    }

    /// <summary>
    /// Initialize geofence states silently from an initial location.
    /// This sets the initial Enter/Approach/Exit state WITHOUT firing events,
    /// preventing auto-play when the user starts the app already inside a zone.
    /// </summary>
    public void InitializeFromLocation(LocationData initialLocation)
    {
        lock (_lock)
        {
            foreach (var poi in _monitoredPOIs.Where(p => p.IsActive))
            {
                var distance = initialLocation.DistanceTo(poi.Latitude, poi.Longitude);
                // Only use admin-configured radii - no fallback defaults
                var triggerRadius = poi.TriggerRadius;
                var approachRadius = poi.ApproachRadius;

                // Skip POIs without properly configured radii
                if (triggerRadius <= 0 || approachRadius <= 0)
                    continue;

                var state = _poiStates.GetOrAdd(poi.Id, _ => new GeofenceState());
                state.Distance = distance;

                // Set initial state SILENTLY - no events fired
                if (distance <= triggerRadius)
                {
                    state.CurrentState = GeofenceEventType.Enter;
                    state.EnterTime = DateTime.UtcNow;
                }
                else if (distance <= approachRadius)
                {
                    state.CurrentState = GeofenceEventType.Approach;
                }
                else
                {
                    state.CurrentState = GeofenceEventType.Exit;
                }
            }

            // Mark as initialized so future events will fire
            IsInitialized = true;
        }
    }

    public async Task ProcessLocationUpdateAsync(LocationData location)
    {
        await Task.Run(() =>
        {
            POI? nearest = null;
            double nearestDistance = double.MaxValue;
            var events = new List<GeofenceEvent>();

            lock (_lock)
            {
                foreach (var poi in _monitoredPOIs.Where(p => p.IsActive))
                {
                    var distance = location.DistanceTo(poi.Latitude, poi.Longitude);
                    // Only use admin-configured radii - no fallback defaults
                    var triggerRadius = poi.TriggerRadius;
                    var approachRadius = poi.ApproachRadius;

                    // Skip POIs without properly configured radii
                    if (triggerRadius <= 0 || approachRadius <= 0)
                        continue;

                    // Tìm POI gần nhất
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = poi;
                    }

                    var state = _poiStates.GetOrAdd(poi.Id, _ => new GeofenceState());
                    state.Distance = distance;
                    var previousState = state.CurrentState;

                    // Xác định trạng thái mới
                    GeofenceEventType? newState = null;

                    if (distance <= triggerRadius)
                    {
                        // Trong vùng trigger
                        if (previousState != GeofenceEventType.Enter && previousState != GeofenceEventType.Dwell)
                        {
                            newState = GeofenceEventType.Enter;
                        }
                        else if (previousState == GeofenceEventType.Enter)
                        {
                            // Đã ở trong vùng một lúc -> Dwell
                            if ((DateTime.UtcNow - state.EnterTime) > TimeSpan.FromSeconds(10))
                            {
                                newState = GeofenceEventType.Dwell;
                            }
                        }
                    }
                    else if (distance <= approachRadius)
                    {
                        // Trong vùng approach
                        if (previousState != GeofenceEventType.Approach && 
                            previousState != GeofenceEventType.Enter && 
                            previousState != GeofenceEventType.Dwell)
                        {
                            newState = GeofenceEventType.Approach;
                        }
                    }
                    else
                    {
                        // Ngoài vùng
                        if (previousState == GeofenceEventType.Enter || 
                            previousState == GeofenceEventType.Dwell ||
                            previousState == GeofenceEventType.Approach)
                        {
                            newState = GeofenceEventType.Exit;
                        }
                    }

                    // Tạo event nếu có thay đổi
                    if (newState.HasValue)
                    {
                        // Enter chịu cooldown + debounce, còn Exit phải được phát ngay để dừng audio đúng lúc.
                        if (newState.Value == GeofenceEventType.Enter)
                        {
                            if (IsCooldownActive(poi.Id) || !CanTrigger(poi.Id))
                            {
                                continue;
                            }
                        }
                        else if (newState.Value != GeofenceEventType.Exit && !CanTrigger(poi.Id))
                        {
                            continue;
                        }

                        state.CurrentState = newState.Value;
                        
                        if (newState.Value == GeofenceEventType.Enter)
                        {
                            state.EnterTime = DateTime.UtcNow;
                        }

                        _lastTriggerTime[poi.Id] = DateTime.UtcNow;

                        events.Add(new GeofenceEvent
                        {
                            POI = poi,
                            EventType = newState.Value,
                            Distance = distance,
                            Location = location,
                            Timestamp = DateTime.UtcNow
                        });

                        // Tự động set cooldown sau khi Enter
                        if (newState.Value == GeofenceEventType.Enter)
                        {
                            var effectiveCooldownSeconds = Math.Clamp(
                                poi.CooldownSeconds,
                                MinEffectiveCooldownSeconds,
                                MaxEffectiveCooldownSeconds);

                            SetCooldown(poi.Id, TimeSpan.FromSeconds(effectiveCooldownSeconds));
                        }
                        // Reset cooldown khi user exit vùng POI - cho phép phát lại khi quay lại
                        else if (newState.Value == GeofenceEventType.Exit)
                        {
                            ResetCooldown(poi.Id);
                        }
                    }
                }
            }

            NearestPOI = nearest;
            NearestPOIDistance = nearest != null ? nearestDistance : null;

            // Fire events - Exit events fire immediately, but Enter events are throttled
            // to only the best candidate (nearest + highest priority) to prevent
            // rapid switching between overlapping POI zones.
            // Skip firing events until geofence is initialized to prevent auto-play on app start.
            if (!IsInitialized)
            {
                return;
            }

            var enterEvents = events.Where(e => e.EventType == GeofenceEventType.Enter).ToList();
            var nonEnterEvents = events.Where(e => e.EventType != GeofenceEventType.Enter);

            // Fire all non-Enter events (Exit, Approach, Dwell)
            foreach (var evt in nonEnterEvents)
            {
                GeofenceTriggered?.Invoke(this, evt);
            }

            // For Enter events, only fire the best candidate:
            // - Smallest distance (nearest)
            // - If tie, highest priority
            if (enterEvents.Any())
            {
                var bestEnter = enterEvents
                    .OrderBy(e => e.Distance)                    // Nearest first
                    .ThenByDescending(e => e.POI.Priority)       // Highest priority first
                    .First();
                
                GeofenceTriggered?.Invoke(this, bestEnter);
            }
        });
    }

    public List<POI> GetPOIsInRange(double radius)
    {
        lock (_lock)
        {
            return _monitoredPOIs
                .Where(p => p.IsActive && _poiStates.TryGetValue(p.Id, out var state) && state.Distance <= radius)
                .OrderBy(p => _poiStates[p.Id].Distance)
                .ToList();
        }
    }

    public void SetCooldown(int poiId, TimeSpan duration)
    {
        _cooldowns[poiId] = DateTime.UtcNow.Add(duration);
    }

    public bool IsCooldownActive(int poiId)
    {
        if (_cooldowns.TryGetValue(poiId, out var expiry))
        {
            return DateTime.UtcNow < expiry;
        }
        return false;
    }

    public void ResetCooldown(int poiId)
    {
        _cooldowns.TryRemove(poiId, out _);
    }

    public void ResetAllCooldowns()
    {
        _cooldowns.Clear();
    }

    private bool CanTrigger(int poiId)
    {
        if (_lastTriggerTime.TryGetValue(poiId, out var lastTime))
        {
            return (DateTime.UtcNow - lastTime) >= _debounceTime;
        }
        return true;
    }

    private class GeofenceState
    {
        public GeofenceEventType? CurrentState { get; set; }
        public double Distance { get; set; } = double.MaxValue;
        public DateTime EnterTime { get; set; }
    }
}
