using ZoneGuide.Shared.Interfaces;
using Plugin.Maui.Audio;

namespace ZoneGuide.Mobile.Services;

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
    private readonly HttpClient _httpClient = new();
    private IAudioPlayer? _player;
    private Stream? _activeStream;
    private CancellationTokenSource? _progressCts;
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
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
        await _playbackGate.WaitAsync();
        try
        {
            // Clean up old player first
            await CleanupPlayerAsync();

            if (!File.Exists(filePath))
            {
                PlaybackError?.Invoke(this, $"File không tồn tại: {filePath}");
                return;
            }

            var stream = File.OpenRead(filePath);
            IAudioPlayer? player = null;

            try
            {
                player = _audioManager.CreatePlayer(stream);
                _activeStream = stream;
                _player = player;
            }
            catch
            {
                player?.Dispose();
                stream.Dispose();
                throw;
            }

            SetupPlayer(player);
            player.Play();
            
            IsPaused = false;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
            
            StartProgressTracking(player);
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, ex.Message);
            await CleanupPlayerAsync();
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    public async Task PlayFromUrlAsync(string url)
    {
        await _playbackGate.WaitAsync();
        try
        {
            // Clean up old player first
            await CleanupPlayerAsync();

            Stream stream = await _httpClient.GetStreamAsync(url);
            IAudioPlayer? player = null;

            try
            {
                player = _audioManager.CreatePlayer(stream);
            }
            catch (Exception)
            {
                stream.Dispose();

                using var sourceStream = await _httpClient.GetStreamAsync(url);
                var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                stream = memoryStream;
                player = _audioManager.CreatePlayer(stream);
            }

            _activeStream = stream;
            _player = player;
            
            SetupPlayer(player);
            player.Play();
            
            IsPaused = false;
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
            
            StartProgressTracking(player);
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke(this, ex.Message);
            await CleanupPlayerAsync();
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    public async Task PauseAsync()
    {
        await _playbackGate.WaitAsync();
        try
        {
            if (_player?.IsPlaying == true)
            {
                _player.Pause();
                IsPaused = true;
                StopProgressTracking();
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _playbackGate.WaitAsync();
        try
        {
            if (_player != null && IsPaused)
            {
                _player.Play();
                IsPaused = false;
                StartProgressTracking(_player);
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _playbackGate.WaitAsync();
        try
        {
            await CleanupPlayerAsync();
        }
        finally
        {
            _playbackGate.Release();
        }
    }

    private Task CleanupPlayerAsync()
    {
        StopProgressTracking();

        var player = _player;
        _player = null;

        if (player != null)
        {
            try
            {
                player.PlaybackEnded -= OnPlaybackEnded;
                player.Stop();
            }
            catch { /* Ignore disposal errors */ }
            finally
            {
                try
                {
                    player.Dispose();
                }
                catch
                {
                    // Ignore disposal errors from native backends
                }
            }
        }

        DisposeActiveStream();
        
        IsPaused = false;
        return Task.CompletedTask;
    }

    public Task SeekAsync(double position)
    {
        var player = _player;
        if (player != null)
        {
            try
            {
                player.Seek(position);
            }
            catch (ObjectDisposedException)
            {
                // Ignore seek on a player that was just cleaned up
            }
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

    private void SetupPlayer(IAudioPlayer player)
    {
        player.Volume = _volume;
        player.PlaybackEnded += OnPlaybackEnded;
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        if (sender != null && !ReferenceEquals(sender, _player))
            return;

        StopProgressTracking();
        IsPaused = false;
        DisposeActiveStream();
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void StartProgressTracking(IAudioPlayer player)
    {
        StopProgressTracking();
        
        _progressCts = new CancellationTokenSource();
        var token = _progressCts.Token;
        
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested && ReferenceEquals(player, _player) && player.IsPlaying)
                {
                    try
                    {
                        if (player.Duration > 0)
                        {
                            var progress = player.CurrentPosition / player.Duration;
                            ProgressChanged?.Invoke(this, progress);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    await Task.Delay(100, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation from stop/restart
            }
            catch (ObjectDisposedException)
            {
                // Native audio backend may dispose while the progress loop is exiting
            }
        }, token);
    }

    private void StopProgressTracking()
    {
        if (_progressCts == null)
            return;

        try
        {
            _progressCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore races with shutdown
        }

        _progressCts.Dispose();
        _progressCts = null;
    }

    private void DisposeActiveStream()
    {
        _activeStream?.Dispose();
        _activeStream = null;
    }

    public void Dispose()
    {
        StopProgressTracking();
        _player?.Dispose();
        DisposeActiveStream();
        _httpClient.Dispose();
        _playbackGate.Dispose();
    }
}
