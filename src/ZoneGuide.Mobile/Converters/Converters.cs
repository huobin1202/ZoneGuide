using System.Globalization;
using ZoneGuide.Mobile.Localization;
using ZoneGuide.Mobile.Services;

namespace ZoneGuide.Mobile.Converters;

/// <summary>
/// Kiểm tra object không null
/// </summary>
public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Đảo ngược bool
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

/// <summary>
/// Bool to Color
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Green;
    public Color FalseColor { get; set; } = Colors.Gray;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? TrueColor : FalseColor;
        return FalseColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Màu nền khi tracking
/// </summary>
public class TrackingColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTracking)
            return isTracking ? Color.FromArgb("#4CAF50") : Color.FromArgb("#512BD4");
        return Color.FromArgb("#512BD4");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Icon khi tracking
/// </summary>
public class TrackingIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTracking)
            return isTracking ? "📍" : "🎯";
        return "🎯";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Text nút tracking
/// </summary>
public class TrackingButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTracking)
            return isTracking
                ? $"⏹ {AppLocalizer.Instance.Translate("tracking_stop", "Stop tracking")}"
                : $"▶️ {AppLocalizer.Instance.Translate("tracking_start", "Start tracking")}";
        return $"▶️ {AppLocalizer.Instance.Translate("tracking_start", "Start tracking")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Màu nút tracking
/// </summary>
public class TrackingButtonColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTracking)
            return isTracking ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50");
        return Color.FromArgb("#4CAF50");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Icon Play/Pause
/// </summary>
public class PlayPauseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
            return isPlaying ? "⏸" : "▶";
        return "▶";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Text nút Sync
/// </summary>
public class SyncButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSyncing)
            return isSyncing
                ? $"⏳ {AppLocalizer.Instance.Translate("sync_in_progress", "Syncing...")}"
                : $"🔄 {AppLocalizer.Instance.Translate("settings_sync_button", "Sync data")}";
        return $"🔄 {AppLocalizer.Instance.Translate("settings_sync_button", "Sync data")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Enum to Bool cho RadioButton
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return null;
    }
}

/// <summary>
/// Text trạng thái offline
/// </summary>
public class OfflineStatusTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOffline)
            return isOffline
                ? $"✅ {AppLocalizer.Instance.Translate("tour_detail_offline_ready", "Saved for offline")}"
                : $"📥 {AppLocalizer.Instance.Translate("tour_detail_offline_prompt", "Not downloaded yet")}";
        return $"📥 {AppLocalizer.Instance.Translate("tour_detail_offline_prompt", "Not downloaded yet")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Text nút offline
/// </summary>
public class OfflineButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOffline)
            return isOffline
                ? AppLocalizer.Instance.Translate("tour_detail_remove_offline", "Remove")
                : AppLocalizer.Instance.Translate("tour_detail_download_offline", "Download");
        return AppLocalizer.Instance.Translate("tour_detail_download_offline", "Download");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Chuyển chuỗi URL/base64/file path thành ImageSource để hiển thị ảnh ổn định trên mobile.
/// </summary>
public class FlexibleImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
            return "location.svg";

        raw = raw.Trim();

        if (raw.StartsWith("/", StringComparison.Ordinal) && !raw.Contains("://", StringComparison.Ordinal))
        {
            raw = raw[1..];
        }

        try
        {
            if (raw.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = raw.IndexOf(',');
                if (commaIndex > 0)
                {
                    var base64 = raw[(commaIndex + 1)..];
                    var bytes = System.Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }

            if (File.Exists(raw))
                return ImageSource.FromFile(raw);

            var normalized = ApiService.NormalizeMediaUrl(raw);
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                return ImageSource.FromUri(uri);

            if (File.Exists(normalized))
                return ImageSource.FromFile(normalized);
        }
        catch
        {
            return "location.svg";
        }

        return "location.svg";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LocalizedCategoryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string category)
            return AppLocalizer.Instance.TranslateCategory(category);

        return AppLocalizer.Instance.Translate("category_other");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
