using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Views;

namespace ZoneGuide.Mobile.ViewModels;

public partial class MoreViewModel : ObservableObject
{
    public ObservableCollection<MoreMenuItemViewModel> MenuItems { get; } = new();

    public MoreViewModel()
    {
        BuildMenu();
        AppLocalizer.Instance.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                return;

            MainThread.BeginInvokeOnMainThread(BuildMenu);
        };
    }

    [RelayCommand]
    private async Task OpenItemAsync(MoreMenuItemViewModel? item)
    {
        if (item == null)
            return;

        await Shell.Current.GoToAsync(item.Route);
    }

    private void BuildMenu()
    {
        MenuItems.Clear();

        MenuItems.Add(new MoreMenuItemViewModel
        {
            Title = AppLocalizer.Instance.Translate("history_title", "History"),
            Subtitle = AppLocalizer.Instance.Translate("more_history_subtitle", "Review what you've listened to recently."),
            IconGlyph = "◷",
            Route = nameof(HistoryPage)
        });

        MenuItems.Add(new MoreMenuItemViewModel
        {
            Title = AppLocalizer.Instance.Translate("offline_title", "Offline"),
            Subtitle = AppLocalizer.Instance.Translate("more_offline_subtitle", "Manage downloaded content on this device."),
            IconGlyph = "↓",
            Route = nameof(OfflinePage)
        });

        MenuItems.Add(new MoreMenuItemViewModel
        {
            Title = AppLocalizer.Instance.Translate("settings_title", "Settings"),
            Subtitle = AppLocalizer.Instance.Translate("more_settings_subtitle", "Adjust audio, GPS, and sync behavior."),
            IconGlyph = "⚙",
            Route = nameof(SettingsPage)
        });
    }
}

public sealed class MoreMenuItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}
