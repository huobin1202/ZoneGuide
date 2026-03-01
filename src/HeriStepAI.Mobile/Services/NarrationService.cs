using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;
using System.Collections.Concurrent;

namespace HeriStepAI.Mobile.Services;

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
    
    private readonly ConcurrentQueue<NarrationQueueItem> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _lock = new();
    
    private NarrationQueueItem? _currentItem;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isProcessing;

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public NarrationQueueItem? CurrentItem => _currentItem;
    public IReadOnlyList<NarrationQueueItem> Queue => _queue.ToList().AsReadOnly();
    public double CurrentProgress { get; private set; }

    public NarrationService(ITTSService ttsService, IAudioService audioService)
    {
        _ttsService = ttsService;
        _audioService = audioService;

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

        if (_currentItem != null)
        {
            _currentItem.Status = NarrationStatus.Paused;
        }

        if (!string.IsNullOrEmpty(_currentItem?.AudioPath))
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

        if (_currentItem != null)
        {
            var stoppedItem = _currentItem;
            stoppedItem.Status = NarrationStatus.Cancelled;
            
            await _ttsService.StopAsync();
            await _audioService.StopAsync();
            
            NarrationStopped?.Invoke(this, stoppedItem);
            _currentItem = null;
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
                _currentItem.Status = NarrationStatus.Playing;
                
                IsPlaying = true;
                IsPaused = false;
                CurrentProgress = 0;

                NarrationStarted?.Invoke(this, _currentItem);

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
                        _currentItem.Status = NarrationStatus.Completed;
                        NarrationCompleted?.Invoke(this, _currentItem);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _currentItem.Status = NarrationStatus.Error;
                    NarrationError?.Invoke(this, ex.Message);
                }

                _currentItem = null;
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

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }
}
