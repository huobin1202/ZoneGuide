using CommunityToolkit.Mvvm.ComponentModel;

namespace ZoneGuide.Mobile.ViewModels;

public partial class LanguageOptionItem : ObservableObject
{
    public string Code { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    public override string ToString() => DisplayName;

    public static List<LanguageOptionItem> CreateDefaults(string? selectedCode = "vi-VN")
    {
        return
        [
            Create("vi-VN", "VN", "Tiếng Việt", "Vietnamese", selectedCode),
            Create("en-US", "GB", "English", "English", selectedCode),
            Create("zh-CN", "CN", "中文", "Chinese", selectedCode),
            Create("ja-JP", "JP", "日本語", "Japanese", selectedCode),
            Create("ko-KR", "KR", "한국어", "Korean", selectedCode),
            Create("fr-FR", "FR", "Français", "French", selectedCode)
        ];
    }

    public static string GetDisplayName(string? code)
    {
        return CreateDefaults(code)
            .FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? (string.IsNullOrWhiteSpace(code) ? "Tiếng Việt" : code);
    }

    private static LanguageOptionItem Create(string code, string shortCode, string displayName, string subtitle, string? selectedCode)
    {
        return new LanguageOptionItem
        {
            Code = code,
            ShortCode = shortCode,
            DisplayName = displayName,
            Subtitle = subtitle,
            IsSelected = string.Equals(code, selectedCode, StringComparison.OrdinalIgnoreCase)
        };
    }
}
