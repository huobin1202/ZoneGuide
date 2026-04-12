using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;
using System.Collections.Concurrent;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service quản lý hàng đợi thuyết minh - chống trùng lặp, đa tiến trình
/// </summary>
public class NarrationService : INarrationService, IDisposable
{
    public event EventHandler<NarrationQueueItem>? NarrationStarted;
    public event EventHandler<NarrationQueueItem>? NarrationCompleted;
    public event EventHandler<NarrationQueueItem>? NarrationStopped;
    public event EventHandler<string>? NarrationError;
    public event EventHandler<double>? ProgressUpdated;

    private readonly ITTSService _ttsService;
    private readonly IAudioService _audioService;
    private readonly IGeofenceService _geofenceService;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly IPOITranslationRepository _poiTranslationRepository;
    private readonly ISettingsService _settingsService;
    private readonly ApiService _apiService;
    
    private readonly ConcurrentQueue<NarrationQueueItem> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _lock = new();
    
    private NarrationQueueItem? _currentItem;
    private NarrationHistory? _activeHistory;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isProcessing;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public NarrationQueueItem? CurrentItem => _currentItem;
    public IReadOnlyList<NarrationQueueItem> Queue => _queue.ToList().AsReadOnly();
    public double CurrentProgress { get; private set; }

    public NarrationService(
        ITTSService ttsService,
        IAudioService audioService,
        IGeofenceService geofenceService,
        IAnalyticsRepository analyticsRepository,
        IPOITranslationRepository poiTranslationRepository,
        ISettingsService settingsService,
        ApiService apiService)
    {
        _ttsService = ttsService;
        _audioService = audioService;
        _geofenceService = geofenceService;
        _analyticsRepository = analyticsRepository;
        _poiTranslationRepository = poiTranslationRepository;
        _settingsService = settingsService;
        _apiService = apiService;

        // Subscribe to events
        _ttsService.SpeakCompleted += OnTTSCompleted;
        _audioService.PlaybackCompleted += OnAudioCompleted;
        _audioService.ProgressChanged += OnProgressChanged;
    }

    public Task EnqueueAsync(NarrationQueueItem item)
    {
        // Kiểm tra trùng lặp
        if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(q => q.POI.Id == item.POI.Id))
        {
            return Task.CompletedTask; // Đã có trong queue hoặc đang phát
        }

        item.Status = NarrationStatus.Queued;
        item.QueuedAt = DateTime.UtcNow;
        
        _queue.Enqueue(item);

        // Chạy nền để không block UI thread và không phụ thuộc caller await.
        EnsureQueueProcessingStarted();
        return Task.CompletedTask;
    }

    public async Task PlayImmediatelyAsync(NarrationQueueItem item)
    {
        // Dừng cái hiện tại
        await StopAsync();

        // Clear queue và thêm item mới vào đầu
        ClearQueue();
        
        item.Status = NarrationStatus.Queued;
        item.QueuedAt = DateTime.UtcNow;
        
        _queue.Enqueue(item);

        // Chạy nền, không chờ phát xong để caller tiếp tục luồng UI/geofence.
        EnsureQueueProcessingStarted();
    }

    public async Task ResumeAsync()
    {
        if (!IsPaused || _currentItem == null)
            return;

        var currentItem = _currentItem;

        if (!string.IsNullOrWhiteSpace(currentItem.AudioPath) ||
            !string.IsNullOrWhiteSpace(currentItem.AudioUrl) ||
            _audioService.IsPaused)
        {
            await _audioService.ResumeAsync();

            if (_audioService.IsPlaying)
            {
                IsPaused = false;
                IsPlaying = true;
                return;
            }

            // Neu resume khong duoc (tuong thich thiet bi/codec), fallback ve phat lai item hien tai.
            await PlayImmediatelyAsync(currentItem);
            IsPaused = false;
            IsPlaying = true;
            return;
        }

        // TTS khong ho tro resume, nen phat lai tu dau.
        if (!string.IsNullOrWhiteSpace(currentItem.TTSText))
        {
            await PlayImmediatelyAsync(currentItem);
            return;
        }

        IsPaused = false;
        IsPlaying = true;
    }

    public async Task PauseAsync()
    {
        if (!IsPlaying)
            return;

        var currentItem = _currentItem;
        if (currentItem != null)
        {
            currentItem.Status = NarrationStatus.Paused;
        }

        var hasAudioSource = !string.IsNullOrWhiteSpace(currentItem?.AudioPath) ||
                             !string.IsNullOrWhiteSpace(currentItem?.AudioUrl) ||
                             _audioService.IsPlaying ||
                             _audioService.IsPaused;

        if (hasAudioSource)
        {
            await _audioService.PauseAsync();
        }
        else
        {
            await _ttsService.StopAsync();
        }

        IsPlaying = false;
        IsPaused = true;
    }

    public async Task RewindAsync(double seconds)
    {
        if (seconds <= 0)
            return;

        if (_currentItem == null)
            return;

        var duration = _audioService.Duration;
        var currentPosition = _audioService.CurrentPosition;

        if (duration <= 0 || currentPosition <= 0)
            return;

        var targetPosition = Math.Max(0, currentPosition - seconds);
        await _audioService.SeekAsync(targetPosition);

        CurrentProgress = duration > 0 ? Math.Clamp(targetPosition / duration, 0, 1) : 0;
        ProgressUpdated?.Invoke(this, CurrentProgress);
    }

    public async Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();

        var currentItem = _currentItem;

        if (currentItem != null)
        {
            currentItem.Status = NarrationStatus.Cancelled;
            
            await _ttsService.StopAsync();
            await _audioService.StopAsync();
            await CloseHistoryRecordAsync(false);
            _geofenceService.ResetCooldown(currentItem.POI.Id);

            IsPlaying = false;
            IsPaused = false;
            CurrentProgress = 0;
            
            NarrationStopped?.Invoke(this, currentItem);

            if (ReferenceEquals(_currentItem, currentItem))
            {
                _currentItem = null;
            }
        }

        IsPlaying = false;
        IsPaused = false;
        CurrentProgress = 0;
    }

    public async Task SkipAsync()
    {
        await StopAsync();
        EnsureQueueProcessingStarted();
    }

    public void ClearQueue()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    public void SetVolume(float volume)
    {
        _ttsService.SetVolume(volume);
        _audioService.SetVolume(volume);
    }

    public void SetTTSSpeed(float speed)
    {
        _ttsService.SetSpeed(speed);
    }

    public async Task<List<string>> GetAvailableVoicesAsync()
    {
        return await _ttsService.GetSupportedLanguagesAsync();
    }

    public async Task SetVoiceAsync(string voiceId)
    {
        _ttsService.SetVoice(voiceId);
        await Task.CompletedTask;
    }

    private async Task ProcessQueueAsync()
    {
        await _semaphore.WaitAsync();
        
        try
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            while (!_cancellationTokenSource.Token.IsCancellationRequested &&
                   _queue.TryDequeue(out var item))
            {
                await ApplyPreferredLanguageContentAsync(item);

                _currentItem = item;
                item.Status = NarrationStatus.Playing;
                await StartHistoryRecordAsync(item);
                
                IsPlaying = true;
                IsPaused = false;
                CurrentProgress = 0;

                NarrationStarted?.Invoke(this, item);

                try
                {
                    var played = false;

                    // Ưu tiên phát file audio nếu có
                    if (!string.IsNullOrEmpty(item.AudioPath) && File.Exists(item.AudioPath))
                    {
                        try
                        {
                            await PlayAudioAndWaitAsync(
                                () => _audioService.PlayAsync(item.AudioPath),
                                _cancellationTokenSource.Token);
                            played = true;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException && !string.IsNullOrWhiteSpace(item.TTSText))
                        {
                            await PlayTtsAndWaitAsync(item, _cancellationTokenSource.Token);
                            played = true;
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.AudioUrl))
                    {
                        try
                        {
                            // Phát audio từ URL online
                            await PlayAudioAndWaitAsync(
                                () => _audioService.PlayFromUrlAsync(item.AudioUrl),
                                _cancellationTokenSource.Token);
                            played = true;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException && !string.IsNullOrWhiteSpace(item.TTSText))
                        {
                            await PlayTtsAndWaitAsync(item, _cancellationTokenSource.Token);
                            played = true;
                        }
                    }

                    if (!played && !string.IsNullOrWhiteSpace(item.TTSText))
                    {
                        await PlayTtsAndWaitAsync(item, _cancellationTokenSource.Token);
                    }

                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        item.Status = NarrationStatus.Completed;
                        await CloseHistoryRecordAsync(true);
                        _geofenceService.ResetCooldown(item.POI.Id);

                        // Update state before raising completion event so ViewModels
                        // don't read stale IsPlaying=true and keep pause icon.
                        IsPlaying = false;
                        IsPaused = false;
                        CurrentProgress = 1.0;

                        NarrationCompleted?.Invoke(this, item);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    item.Status = NarrationStatus.Error;
                    await CloseHistoryRecordAsync(false);
                    NarrationError?.Invoke(this, ex.Message);
                }

                if (ReferenceEquals(_currentItem, item))
                {
                    _currentItem = null;
                }
            }
        }
        finally
        {
            _isProcessing = false;
            IsPlaying = false;
            _semaphore.Release();
        }
    }

    private void EnsureQueueProcessingStarted()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessQueueAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NarrationService] EnsureQueueProcessingStarted failed: {ex.Message}");
                NarrationError?.Invoke(this, ex.Message);
            }
        });
    }

    private async Task PlayTtsAndWaitAsync(NarrationQueueItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.TTSText))
            return;

        await _ttsService.SpeakAsync(item.TTSText, item.Language);

        while (_ttsService.IsSpeaking && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task PlayAudioAndWaitAsync(Func<Task> playAction, CancellationToken cancellationToken)
    {
        var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPlaybackCompleted(object? sender, EventArgs e)
        {
            completionTcs.TrySetResult(true);
        }

        void OnPlaybackError(object? sender, string error)
        {
            completionTcs.TrySetException(new InvalidOperationException(error));
        }

        _audioService.PlaybackCompleted += OnPlaybackCompleted;
        _audioService.PlaybackError += OnPlaybackError;

        try
        {
            await playAction();

            var startupTimeout = TimeSpan.FromSeconds(2);
            var startupAt = DateTime.UtcNow;

            while (!_audioService.IsPlaying &&
                   !_audioService.IsPaused &&
                   !completionTcs.Task.IsCompleted &&
                   DateTime.UtcNow - startupAt < startupTimeout &&
                   !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(50, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!_audioService.IsPlaying && !_audioService.IsPaused && !completionTcs.Task.IsCompleted)
            {
                throw new InvalidOperationException("Audio did not start playback.");
            }

            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var finishedTask = await Task.WhenAny(completionTcs.Task, cancellationTask);

            if (finishedTask == cancellationTask)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            await completionTcs.Task;
        }
        finally
        {
            _audioService.PlaybackCompleted -= OnPlaybackCompleted;
            _audioService.PlaybackError -= OnPlaybackError;
        }
    }

    private void OnTTSCompleted(object? sender, EventArgs e)
    {
        CurrentProgress = 1.0;
        ProgressUpdated?.Invoke(this, 1.0);
    }

    private void OnAudioCompleted(object? sender, EventArgs e)
    {
        CurrentProgress = 1.0;
        ProgressUpdated?.Invoke(this, 1.0);
    }

    private void OnProgressChanged(object? sender, double progress)
    {
        CurrentProgress = progress;
        ProgressUpdated?.Invoke(this, progress);
    }

    private async Task StartHistoryRecordAsync(NarrationQueueItem item)
    {
        try
        {
            _activeHistory = new NarrationHistory
            {
                AnonymousDeviceId = await GetAnonymousDeviceIdAsync(),
                SessionId = _sessionId,
                POIId = item.POI.Id,
                POIName = item.POI.Name,
                Language = string.IsNullOrWhiteSpace(item.Language)
                    ? _settingsService.Settings.PreferredLanguage
                    : item.Language,
                StartTime = DateTime.UtcNow,
                TriggerType = item.TriggerType.ToString(),
                TriggerDistance = item.TriggerDistance,
                TriggerLatitude = 0,
                TriggerLongitude = 0
            };

            await _analyticsRepository.InsertNarrationAsync(_activeHistory);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] StartHistoryRecordAsync failed: {ex.Message}");
            _activeHistory = null;
        }
    }

    private async Task CloseHistoryRecordAsync(bool completed)
    {
        if (_activeHistory == null)
            return;

        try
        {
            var endedAt = DateTime.UtcNow;
            _activeHistory.EndTime = endedAt;
            _activeHistory.DurationSeconds = Math.Max(1, (int)Math.Round((endedAt - _activeHistory.StartTime).TotalSeconds));
            _activeHistory.TotalDurationSeconds = _activeHistory.DurationSeconds;
            _activeHistory.Completed = completed;

            await _analyticsRepository.UpdateNarrationAsync(_activeHistory);
            await UploadSingleNarrationAsync(_activeHistory);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] CloseHistoryRecordAsync failed: {ex.Message}");
        }
        finally
        {
            _activeHistory = null;
        }
    }

    private async Task<string> GetAnonymousDeviceIdAsync()
    {
        try
        {
            var deviceId = await _settingsService.GetAsync<string>("anonymous_device_id");
            if (!string.IsNullOrWhiteSpace(deviceId))
                return deviceId;

            deviceId = Guid.NewGuid().ToString("N")[..16];
            await _settingsService.SetAsync("anonymous_device_id", deviceId);
            return deviceId;
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..16];
        }
    }

    private async Task UploadSingleNarrationAsync(NarrationHistory history)
    {
        try
        {
            var deviceId = await GetAnonymousDeviceIdAsync();
            var payload = new AnalyticsUploadDto
            {
                AnonymousDeviceId = deviceId,
                Locations = new List<LocationHistoryDto>(),
                Narrations = new List<NarrationHistoryDto>
                {
                    new()
                    {
                        SessionId = history.SessionId,
                        POIId = history.POIId.ToString(),
                        POIName = history.POIName,
                        Language = history.Language,
                        StartTime = history.StartTime,
                        EndTime = history.EndTime,
                        DurationSeconds = history.DurationSeconds,
                        TotalDurationSeconds = history.TotalDurationSeconds,
                        Completed = history.Completed,
                        TriggerType = history.TriggerType,
                        TriggerDistance = history.TriggerDistance,
                        TriggerLatitude = history.TriggerLatitude,
                        TriggerLongitude = history.TriggerLongitude
                    }
                }
            };

            var uploaded = await _apiService.UploadAnalyticsAsync(payload);
            System.Diagnostics.Debug.WriteLine($"[NarrationService] Auto upload analytics: {(uploaded ? "ok" : "failed")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] UploadSingleNarrationAsync failed: {ex.Message}");
        }
    }

    private async Task ApplyPreferredLanguageContentAsync(NarrationQueueItem item)
    {
        var preferredLanguage = NormalizeLanguage(_settingsService.Settings.PreferredLanguage);
        item.Language = preferredLanguage;

        var localAudioPath = ResolveAvailableOfflineAudioPath(item);
        if (!string.IsNullOrWhiteSpace(localAudioPath))
        {
            item.AudioPath = localAudioPath;
            item.POI.AudioFilePath = localAudioPath;
        }

        try
        {
            var translation = await _poiTranslationRepository.GetByPOIIdAndLanguageAsync(item.POI.Id, preferredLanguage);
            if (translation == null)
                return;

            // Khi có bản dịch, ưu tiên dữ liệu audio/text theo ngôn ngữ đã chọn.
            if (string.IsNullOrWhiteSpace(item.AudioPath))
            {
                item.AudioUrl = string.IsNullOrWhiteSpace(translation.AudioUrl)
                    ? item.AudioUrl
                    : translation.AudioUrl;
            }

            item.TTSText = !string.IsNullOrWhiteSpace(translation.TTSScript)
                ? translation.TTSScript
                : item.TTSText;

            if (!string.IsNullOrWhiteSpace(translation.Name))
                item.POI.Name = translation.Name;

            item.POI.Language = preferredLanguage;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] ApplyPreferredLanguageContentAsync failed: {ex.Message}");
        }
    }

    private static string? ResolveAvailableOfflineAudioPath(NarrationQueueItem item)
    {
        var preferredLanguage = NormalizeLanguage(item.Language);
        var candidates = new List<string?>
        {
            item.AudioPath,
            item.POI.AudioFilePath
        };

        if (item.POI.Id > 0)
        {
            if (item.POI.TourId.HasValue && item.POI.TourId.Value > 0)
            {
                candidates.Add(Path.Combine(
                    FileSystem.AppDataDirectory,
                    "offline",
                    "packs",
                    item.POI.Id.ToString(),
                    $"audio_{preferredLanguage.Replace('-', '_')}.mp3"));

                candidates.Add(Path.Combine(
                    FileSystem.AppDataDirectory,
                    "offline",
                    item.POI.TourId.Value.ToString(),
                    $"audio_{preferredLanguage.Replace('-', '_')}.mp3"));

                candidates.Add(Path.Combine(
                    FileSystem.AppDataDirectory,
                    "offline",
                    item.POI.TourId.Value.ToString(),
                    $"audio_{item.POI.Id}.mp3"));
            }

            candidates.Add(Path.Combine(
                FileSystem.AppDataDirectory,
                "offline",
                "packs",
                item.POI.Id.ToString(),
                $"audio_{preferredLanguage.Replace('-', '_')}.mp3"));

            candidates.Add(Path.Combine(
                FileSystem.AppDataDirectory,
                "offline",
                "general",
                $"audio_{item.POI.Id}.mp3"));
        }

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .FirstOrDefault(File.Exists);
    }

    private static string NormalizeLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "vi-VN";

        var value = code.Trim().Replace('_', '-');
        return value.ToLowerInvariant() switch
        {
            var c when c.StartsWith("vi") => "vi-VN",
            var c when c.StartsWith("en") => "en-US",
            var c when c.StartsWith("zh") => "zh-CN",
            var c when c.StartsWith("ja") => "ja-JP",
            var c when c.StartsWith("ko") => "ko-KR",
            var c when c.StartsWith("fr") => "fr-FR",
            _ => value
        };
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }
}
