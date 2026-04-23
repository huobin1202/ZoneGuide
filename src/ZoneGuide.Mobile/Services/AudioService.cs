using ZoneGuide.Shared.Interfaces;
using Plugin.Maui.Audio;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service phát Audio file với caching để tối ưu performance
/// </summary>
public class AudioService : IAudioService, IDisposable
{
    private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromMilliseconds(250);
    private const double ProgressChangeThreshold = 0.01;
    private const int MaxCacheSizeMB = 100; // Giới hạn cache 100MB
    private const string AudioCacheDir = "audio_cache";

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackCompleted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<string>? PlaybackError;

    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient = new();
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, string> _memoryCache = new(); // URL -> file path
    private readonly object _cacheLock = new();
    private long _currentCacheSizeBytes = 0;
    
    private IAudioPlayer? _player;
    private Stream? _activeStream;
    private CancellationTokenSource? _progressCts;
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private float _volume = 1.0f;
    private bool _isStopping;
    private double _lastReportedProgress = -1;

    public bool IsPlaying => _player?.IsPlaying ?? false;
    public bool IsPaused { get; private set; }
    public double CurrentPosition => _player?.CurrentPosition ?? 0;
    public double Duration => _player?.Duration ?? 0;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        
        // Initialize cache directory
        _cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, AudioCacheDir);
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
        
        // Load existing cache index
        LoadCacheIndex();
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

            Stream stream;
            var cachedPath = GetCachedAudioPath(url);
            
            if (cachedPath != null)
            {
                // Use cached file
                stream = File.OpenRead(cachedPath);
            }
            else
            {
                // Download and cache
                stream = await DownloadAndCacheAsync(url);
            }
            
            IAudioPlayer? player = null;
            try
            {
                player = _audioManager.CreatePlayer(stream);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
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
    
    /// <summary>
    /// Downloads audio from URL and caches it to disk for future playback.
    /// </summary>
    private async Task<Stream> DownloadAndCacheAsync(string url)
    {
        try
        {
            // Generate cache file name from URL hash
            var urlHash = Convert.ToBase64String(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(url)));
            var cacheFileName = $"{urlHash}.mp3";
            var cacheFilePath = Path.Combine(_cacheDirectory, cacheFileName);
            
            // Download with streaming to avoid memory pressure
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            await using var networkStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(cacheFilePath);
            await networkStream.CopyToAsync(fileStream);
            
            // Add to cache index
            AddToCache(url, cacheFilePath);
            
            // Return file stream for playback
            return File.OpenRead(cacheFilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to download audio: {ex.Message}", ex);
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
        _isStopping = false;
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
        _lastReportedProgress = 1;
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
    
    #region Audio Caching
    
    /// <summary>
    /// Loads the cache index from disk (file names map to URLs in a simple index file).
    /// </summary>
    private void LoadCacheIndex()
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, "cache_index.txt");
            if (File.Exists(indexPath))
            {
                var lines = File.ReadAllLines(indexPath);
                lock (_cacheLock)
                {
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            var url = parts[0];
                            var filePath = parts[1];
                            if (File.Exists(filePath))
                            {
                                _memoryCache[url] = filePath;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore cache load errors
        }
    }
    
    /// <summary>
    /// Saves the cache index to disk.
    /// </summary>
    private void SaveCacheIndex()
    {
        try
        {
            var indexPath = Path.Combine(_cacheDirectory, "cache_index.txt");
            var lines = new List<string>();
            lock (_cacheLock)
            {
                foreach (var kvp in _memoryCache)
                {
                    lines.Add($"{kvp.Key}|{kvp.Value}");
                }
            }
            File.WriteAllLines(indexPath, lines);
        }
        catch
        {
            // Ignore cache save errors
        }
    }
    
    /// <summary>
    /// Gets a cached audio file path for a URL, or null if not cached.
    /// </summary>
    private string? GetCachedAudioPath(string url)
    {
        lock (_cacheLock)
        {
            if (_memoryCache.TryGetValue(url, out var cachedPath) && File.Exists(cachedPath))
            {
                return cachedPath;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Adds an audio file to the cache with LRU eviction.
    /// </summary>
    private void AddToCache(string url, string filePath)
    {
        lock (_cacheLock)
        {
            // If already cached, remove old entry first
            if (_memoryCache.ContainsKey(url))
            {
                RemoveFromCache(url);
            }
            
            // Check cache size limit
            var fileSize = new FileInfo(filePath).Length;
            while (_currentCacheSizeBytes + fileSize > MaxCacheSizeMB * 1024 * 1024 && _memoryCache.Count > 0)
            {
                // Remove oldest entry (simple FIFO - could improve to LRU)
                var firstKey = _memoryCache.Keys.First();
                RemoveFromCache(firstKey);
            }
            
            _memoryCache[url] = filePath;
            _currentCacheSizeBytes += fileSize;
        }
        
        SaveCacheIndex();
    }
    
    /// <summary>
    /// Removes an entry from the cache.
    /// </summary>
    private void RemoveFromCache(string url)
    {
        lock (_cacheLock)
        {
            if (_memoryCache.TryGetValue(url, out var filePath))
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        _currentCacheSizeBytes -= new FileInfo(filePath).Length;
                        File.Delete(filePath);
                    }
                }
                catch { }
                _memoryCache.Remove(url);
            }
        }
    }
    
    #endregion
}
