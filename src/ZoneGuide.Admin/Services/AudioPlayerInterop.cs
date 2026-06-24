namespace ZoneGuide.Admin.Services;

public class AudioTrack
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Language { get; set; }
}

public class AudioPlayerInterop
{
    public event Func<AudioTrack, Task>? OnPlayRequested;
    public event Func<Task>? OnPauseRequested;
    public event Func<Task>? OnStopRequested;
    public event Func<double, Task>? OnSeekRequested;

    public async Task RequestPlayAsync(AudioTrack track)
    {
        if (OnPlayRequested is not null)
            await OnPlayRequested.Invoke(track);
    }

    public async Task RequestPauseAsync()
    {
        if (OnPauseRequested is not null)
            await OnPauseRequested.Invoke();
    }

    public async Task RequestStopAsync()
    {
        if (OnStopRequested is not null)
            await OnStopRequested.Invoke();
    }

    public async Task RequestSeekAsync(double progress)
    {
        if (OnSeekRequested is not null)
            await OnSeekRequested.Invoke(progress);
    }
}
