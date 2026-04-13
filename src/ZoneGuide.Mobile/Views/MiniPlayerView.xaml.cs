using Microsoft.Extensions.DependencyInjection;
using ZoneGuide.Mobile.ViewModels;

namespace ZoneGuide.Mobile.Views;

public partial class MiniPlayerView : ContentView
{
    private bool _bindingInitialized;

    public MiniPlayerView()
    {
        InitializeComponent();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_bindingInitialized || Handler?.MauiContext?.Services == null)
            return;

        BindingContext = Handler.MauiContext.Services.GetRequiredService<GlobalMiniPlayerViewModel>();
        _bindingInitialized = true;
    }

    private GlobalMiniPlayerViewModel? ViewModel => BindingContext as GlobalMiniPlayerViewModel;

    private async void OnTogglePlaybackClicked(object? sender, EventArgs e)
    {
        var command = ViewModel?.TogglePlaybackCommand;
        if (command == null || !command.CanExecute(null))
            return;

        await command.ExecuteAsync(null);
    }

    private async void OnRewindClicked(object? sender, EventArgs e)
    {
        var command = ViewModel?.RewindCommand;
        if (command == null || !command.CanExecute(null))
            return;

        await command.ExecuteAsync(null);
    }

    private async void OnStopPlaybackClicked(object? sender, EventArgs e)
    {
        var command = ViewModel?.StopPlaybackCommand;
        if (command == null || !command.CanExecute(null))
            return;

        await command.ExecuteAsync(null);
    }
}
