using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ZoneGuide.Shared.Interfaces;

namespace ZoneGuide.Mobile.ViewModels;

public partial class LanguageSelectionViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private LanguageOptionItem? selectedLanguage;

    [ObservableProperty]
    private bool isSaving;

    public ObservableCollection<LanguageOptionItem> Languages { get; } = new();

    public event EventHandler? Completed;

    public LanguageSelectionViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        var currentCode = string.IsNullOrWhiteSpace(_settingsService.Settings.PreferredLanguage)
            ? "vi-VN"
            : _settingsService.Settings.PreferredLanguage;

        foreach (var option in LanguageOptionItem.CreateDefaults(currentCode))
        {
            Languages.Add(option);
        }

        SelectedLanguage = Languages.FirstOrDefault(x => x.IsSelected) ?? Languages.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectLanguage(LanguageOptionItem? option)
    {
        if (option == null)
            return;

        foreach (var item in Languages)
        {
            item.IsSelected = ReferenceEquals(item, option);
        }

        SelectedLanguage = option;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (SelectedLanguage == null || IsSaving)
            return;

        IsSaving = true;

        try
        {
            var settings = _settingsService.Settings;
            settings.PreferredLanguage = SelectedLanguage.Code;
            settings.HasCompletedLanguageSelection = true;
            await _settingsService.SaveAsync();

            Completed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
