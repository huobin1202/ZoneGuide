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
    private readonly IAnalyticsRepository _analyticsRepository;
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
        IAnalyticsRepository analyticsRepository,
        ISettingsService settingsService,
        ApiService apiService)
    {
        _ttsService = ttsService;
        _audioService = audioService;
        _analyticsRepository = analyticsRepository;
        _settingsService = settingsService;
        _apiService = apiService;

        // Subscribe to events
        _ttsService.SpeakCompleted += OnTTSCompleted;
        _audioService.PlaybackCompleted += OnAudioCompleted;
        _audioService.ProgressChanged += OnProgressChanged;
    }

    public async Task EnqueueAsync(NarrationQueueItem item)
    {
        // Kiểm tra trùng lặp
        if (_currentItem?.POI.Id == item.POI.Id || _queue.Any(q => q.POI.Id == item.POI.Id))
        {
            return; // Đã có trong queue hoặc đang phát
        }

        item.Status = NarrationStatus.Queued;
        item.QueuedAt = DateTime.UtcNow;
        
        _queue.Enqueue(item);

        // Bắt đầu xử lý nếu chưa có gì đang phát
        if (!_isProcessing && !IsPlaying)
        {
            await ProcessQueueAsync();
        }
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
        
        await ProcessQueueAsync();
    }

    public async Task ResumeAsync()
    {
        if (!IsPaused || _currentItem == null)
            return;

        if (!string.IsNullOrEmpty(_currentItem.AudioPath))
        {
            await _audioService.ResumeAsync();
        }
        // TTS không hỗ trợ resume, phải phát lại

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

        if (!string.IsNullOrEmpty(currentItem?.AudioPath))
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
        await ProcessQueueAsync();
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
        if (_isProcessing)
            return;

        await _semaphore.WaitAsync();
        
        try
        {
            _isProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            while (_queue.TryDequeue(out var item) && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _currentItem = item;
                item.Status = NarrationStatus.Playing;
                await StartHistoryRecordAsync(item);
                
                IsPlaying = true;
                IsPaused = false;
                CurrentProgress = 0;

                NarrationStarted?.Invoke(this, item);

                try
                {
                    // Ưu tiên phát file audio nếu có
                    if (!string.IsNullOrEmpty(item.AudioPath) && File.Exists(item.AudioPath))
                    {
                        await _audioService.PlayAsync(item.AudioPath);
                        
                        // Chờ phát xong
                        while (_audioService.IsPlaying && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100);
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.AudioUrl))
                    {
                        // Phát audio từ URL online
                        await _audioService.PlayFromUrlAsync(item.AudioUrl);
                        
                        // Chờ phát xong
                        while (_audioService.IsPlaying && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100);
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.TTSText))
                    {
                        // Dùng TTS
                        await _ttsService.SpeakAsync(item.TTSText, item.Language);
                        
                        // Chờ nói xong
                        while (_ttsService.IsSpeaking && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100);
                        }
                    }

                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        item.Status = NarrationStatus.Completed;
                        await CloseHistoryRecordAsync(true);
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

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }
}
