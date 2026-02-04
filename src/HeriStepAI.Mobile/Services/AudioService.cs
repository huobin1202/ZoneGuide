using HeriStepAI.Shared.Interfaces;
using Plugin.Maui.Audio;

namespace HeriStepAI.Mobile.Services;

/// <summary>
/// Service phát Audio file
/// </summary>
public class AudioService : IAudioService, IDisposable
{
    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackCompleted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<string>? PlaybackError;

    private readonly IAudioManager _audioManager;
    private IAudioPlayer? _player;
    private CancellationTokenSource? _progressCts;
    private float _volume = 1.0f;

    public bool IsPlaying => _player?.IsPlaying ?? false;
    public bool IsPaused { get; private set; }
    public double CurrentPosition => _player?.CurrentPosition ?? 0;
    public double Duration => _player?.Duration ?? 0;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public async Task PlayAsync(string filePath)
    {
        try
        {
            await StopAsync();

            if (!File.Exists(filePath))
            {
                PlaybackError?.Invoke(this, $"File không tồn tại: {filePath}");
                return;
            }

            var stream = File.OpenRead(filePath);
            _player = _audioManager.CreatePlayer(stream);
            
            SetupPlayer();
            _player.Play();
            
            IsPaused = false;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
            
            StartProgressTracking();
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, ex.Message);
        }
    }

    public async Task PlayFromUrlAsync(string url)
    {
        try
        {
            await StopAsync();

            using var httpClient = new HttpClient();
            var stream = await httpClient.GetStreamAsync(url);
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            _player = _audioManager.CreatePlayer(memoryStream);
            
            SetupPlayer();
            _player.Play();
            
            IsPaused = false;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
            
            StartProgressTracking();
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, ex.Message);
        }
    }

    public Task PauseAsync()
    {
        if (_player?.IsPlaying == true)
        {
            _player.Pause();
            IsPaused = true;
            StopProgressTracking();
            PlaybackPaused?.Invoke(this, EventArgs.Empty);
        }
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (_player != null && IsPaused)
        {
            _player.Play();
            IsPaused = false;
            StartProgressTracking();
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopProgressTracking();
        
        if (_player != null)
        {
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
        
        IsPaused = false;
        return Task.CompletedTask;
    }

    public Task SeekAsync(double position)
    {
        if (_player != null)
        {
            _player.Seek(position);
        }
        return Task.CompletedTask;
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
        if (_player != null)
        {
            _player.Volume = _volume;
        }
    }

    private void SetupPlayer()
    {
        if (_player == null) return;

        _player.Volume = _volume;
        _player.PlaybackEnded += OnPlaybackEnded;
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        StopProgressTracking();
        IsPaused = false;
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void StartProgressTracking()
    {
        StopProgressTracking();
        
        _progressCts = new CancellationTokenSource();
        
        _ = Task.Run(async () =>
        {
            while (!_progressCts.Token.IsCancellationRequested && _player?.IsPlaying == true)
            {
                if (_player.Duration > 0)
                {
                    var progress = _player.CurrentPosition / _player.Duration;
                    ProgressChanged?.Invoke(this, progress);
                }
                await Task.Delay(100);
            }
        }, _progressCts.Token);
    }

    private void StopProgressTracking()
    {
        _progressCts?.Cancel();
        _progressCts?.Dispose();
        _progressCts = null;
    }

    public void Dispose()
    {
        StopProgressTracking();
        _player?.Dispose();
    }
}
