using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.ViewModels;

public partial class GlobalMiniPlayerViewModel : ObservableObject
{
    private const double RewindStepSeconds = 10;
    private readonly INarrationService _narrationService;
    private readonly IAudioService _audioService;
    private Tour? _activeTour;
    private bool _tourSessionActive;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private bool hasActiveTourAudio;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string progressText = string.Empty;

    [ObservableProperty]
    private string elapsedText = "0:00";

    [ObservableProperty]
    private string remainingText = "-0:00";

    public string ToggleGlyph => IsPaused ? "▶" : "⏸";
    public string StopGlyph => "■";
    public string RewindGlyph => "↺10";
    public bool HasKnownTimeline => !string.IsNullOrWhiteSpace(ProgressText) && !ProgressText.EndsWith('%');

    public GlobalMiniPlayerViewModel(INarrationService narrationService, IAudioService audioService)
    {
        _narrationService = narrationService;
        _audioService = audioService;

        _narrationService.NarrationStarted += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
        _narrationService.NarrationStopped += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
        _narrationService.NarrationCompleted += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
        _narrationService.ProgressUpdated += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);

        _audioService.PlaybackStarted += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
        _audioService.PlaybackPaused += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
        _audioService.PlaybackCompleted += (_, _) =>
        {
            _tourSessionActive = false;
            _activeTour = null;
            MainThread.BeginInvokeOnMainThread(RefreshState);
        };
        _audioService.ProgressChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshState);
    }

    public void SetActiveTour(Tour? tour)
    {
        _activeTour = tour;
        _tourSessionActive = tour != null;
        RefreshState();
    }

    public void ClearTourContext()
    {
        _tourSessionActive = false;
        _activeTour = null;
        RefreshState();
    }

    public void RefreshState()
    {
        var currentNarration = _narrationService.CurrentItem;
        var hasNarration = currentNarration != null && (_narrationService.IsPlaying || _narrationService.IsPaused);

        if (hasNarration)
        {
            var poi = currentNarration!.POI;
            HasActiveTourAudio = false;
            IsVisible = true;
            IsPlaying = _narrationService.IsPlaying;
            IsPaused = _narrationService.IsPaused;
            Title = poi.Name;
            Progress = Math.Clamp(_narrationService.CurrentProgress, 0, 1);
            UpdateTimeline(
                _audioService.CurrentPosition,
                _audioService.Duration > 0 ? _audioService.Duration : poi.AudioDurationSeconds);
            OnPropertyChanged(nameof(ToggleGlyph));
            OnPropertyChanged(nameof(StopGlyph));
            OnPropertyChanged(nameof(RewindGlyph));
            OnPropertyChanged(nameof(HasKnownTimeline));
            return;
        }

        var hasTourAudio = _tourSessionActive && _activeTour != null && (_audioService.IsPlaying || _audioService.IsPaused);
        if (hasTourAudio)
        {
            HasActiveTourAudio = true;
            IsVisible = true;
            IsPlaying = _audioService.IsPlaying;
            IsPaused = _audioService.IsPaused;
            Title = _activeTour!.Name;

            var duration = _audioService.Duration;
            var position = _audioService.CurrentPosition;
            Progress = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
            UpdateTimeline(position, duration > 0 ? duration : null);
            OnPropertyChanged(nameof(ToggleGlyph));
            OnPropertyChanged(nameof(StopGlyph));
            OnPropertyChanged(nameof(RewindGlyph));
            OnPropertyChanged(nameof(HasKnownTimeline));
            return;
        }

        HasActiveTourAudio = false;
        IsVisible = false;
        IsPlaying = false;
        IsPaused = false;
        Title = string.Empty;
        Progress = 0;
        ProgressText = string.Empty;
        ElapsedText = "0:00";
        RemainingText = "-0:00";
        OnPropertyChanged(nameof(ToggleGlyph));
        OnPropertyChanged(nameof(StopGlyph));
        OnPropertyChanged(nameof(RewindGlyph));
        OnPropertyChanged(nameof(HasKnownTimeline));
    }

    [RelayCommand]
    private async Task TogglePlaybackAsync()
    {
        var hasNarration = _narrationService.CurrentItem != null && (_narrationService.IsPlaying || _narrationService.IsPaused);
        if (hasNarration)
        {
            if (_narrationService.IsPaused)
                await _narrationService.ResumeAsync();
            else
                await _narrationService.PauseAsync();

            RefreshState();
            return;
        }

        if (_activeTour != null && (_audioService.IsPlaying || _audioService.IsPaused))
        {
            if (_audioService.IsPaused)
                await _audioService.ResumeAsync();
            else
                await _audioService.PauseAsync();

            RefreshState();
        }
    }

    [RelayCommand]
    private async Task StopPlaybackAsync()
    {
        var hasNarration = _narrationService.CurrentItem != null && (_narrationService.IsPlaying || _narrationService.IsPaused);
        if (hasNarration)
        {
            await _narrationService.StopAsync();
            RefreshState();
            return;
        }

        if (_activeTour != null && (_audioService.IsPlaying || _audioService.IsPaused))
        {
            await _audioService.StopAsync();
            ClearTourContext();
        }
    }

    [RelayCommand]
    private async Task RewindAsync()
    {
        var hasNarration = _narrationService.CurrentItem != null && (_narrationService.IsPlaying || _narrationService.IsPaused);
        if (hasNarration)
        {
            await _narrationService.RewindAsync(RewindStepSeconds);
            RefreshState();
            return;
        }

        if (_activeTour != null && (_audioService.IsPlaying || _audioService.IsPaused))
        {
            var targetPosition = Math.Max(0, _audioService.CurrentPosition - RewindStepSeconds);
            await _audioService.SeekAsync(targetPosition);
            RefreshState();
        }
    }

    private void UpdateTimeline(double elapsedSeconds, double? totalSeconds)
    {
        if (!totalSeconds.HasValue || totalSeconds <= 0)
        {
            ProgressText = FormatTime(Math.Max(0, elapsedSeconds));
            ElapsedText = ProgressText;
            RemainingText = "--:--";
            return;
        }

        var boundedElapsed = Math.Clamp(elapsedSeconds, 0, totalSeconds.Value);
        var remaining = Math.Max(0, totalSeconds.Value - boundedElapsed);
        ProgressText = $"{FormatTime(boundedElapsed)} / {FormatTime(totalSeconds.Value)}";
        ElapsedText = FormatTime(boundedElapsed);
        RemainingText = $"-{FormatTime(remaining)}";
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }
}
