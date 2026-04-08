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
            return isTracking ? Color.FromArgb("#4CAF50") : Color.FromArgb("#22C55E");
        return Color.FromArgb("#22C55E");
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

public class OfflineTabTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
            return isSelected ? Colors.White : Color.FromArgb("#475569");

        return Color.FromArgb("#475569");
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

/// <summary>
/// Ưu tiên ảnh local (ImagePath) trước ảnh online (ImageUrl) cho POI.
/// values[0] = ImagePath, values[1] = ImageUrl
/// </summary>
public class PreferredPoiImageSourceConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var imagePath = values.Length > 0 ? values[0] as string : null;
        var imageUrl = values.Length > 1 ? values[1] as string : null;

        return ResolveImageSource(imagePath)
               ?? ResolveImageSource(imageUrl)
               ?? "location.svg";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static ImageSource? ResolveImageSource(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var candidate = raw.Trim();

        try
        {
            if (candidate.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = candidate.IndexOf(',');
                if (commaIndex > 0)
                {
                    var base64 = candidate[(commaIndex + 1)..];
                    var bytes = System.Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }

            if (File.Exists(candidate))
                return ImageSource.FromFile(candidate);

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                    return ImageSource.FromFile(uri.LocalPath);

                return ImageSource.FromUri(uri);
            }

            var localPath = Path.Combine(
                Microsoft.Maui.Storage.FileSystem.AppDataDirectory,
                candidate.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (File.Exists(localPath))
                return ImageSource.FromFile(localPath);

            var normalized = ApiService.NormalizeMediaUrl(candidate);

            if (File.Exists(normalized))
                return ImageSource.FromFile(normalized);

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var normalizedUri))
            {
                if (normalizedUri.IsFile && File.Exists(normalizedUri.LocalPath))
                    return ImageSource.FromFile(normalizedUri.LocalPath);

                return ImageSource.FromUri(normalizedUri);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}

/// <summary>
/// Tính khoảng cách từ vị trí người dùng hiện tại tới POI.
/// values[0] = UserLocation, values[1] = POI.Latitude, values[2] = POI.Longitude
/// </summary>
public class PoiDistanceFromLocationConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3)
            return "--";

        if (values[0] is not Microsoft.Maui.Devices.Sensors.Location userLocation)
            return "--";

        var poiLatitude = TryConvertToDouble(values[1]);
        var poiLongitude = TryConvertToDouble(values[2]);

        if (!poiLatitude.HasValue || !poiLongitude.HasValue)
            return "--";

        var meters = CalculateDistanceMeters(
            userLocation.Latitude,
            userLocation.Longitude,
            poiLatitude.Value,
            poiLongitude.Value);

        if (double.IsNaN(meters) || double.IsInfinity(meters) || meters < 0)
            return "--";

        return DistanceUnitService.FormatAsKilometers(meters);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000d;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var radLat1 = DegreesToRadians(lat1);
        var radLat2 = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(radLat1) * Math.Cos(radLat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value)
    {
        return value * Math.PI / 180d;
    }

    private static double? TryConvertToDouble(object? value)
    {
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            decimal decimalValue => (double)decimalValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
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

public class PreferredDistanceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var meters = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => 0d
        };

        var formatInKilometersOnly = parameter is string p &&
                                     string.Equals(p, "km", StringComparison.OrdinalIgnoreCase);

        var formatted = formatInKilometersOnly
            ? DistanceUnitService.FormatAsKilometers(meters)
            : DistanceUnitService.FormatFromMeters(meters);

        if (parameter is string p2 && string.Equals(p2, "prefix", StringComparison.OrdinalIgnoreCase))
        {
            return $"Cách {formatted}";
        }

        return formatted;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CategoryChipIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
            return "●";

        var category = raw.Trim().ToLowerInvariant();

        return category switch
        {
            "tất cả" or "tat ca" or "all" => "◉",
            "du lịch" or "tourism" => "⌘",
            "dịch vụ" or "dich vu" or "service" or "services" => "⚙",
            "ăn uống" or "an uong" or "food" => "⌂",
            "giải trí" or "giai tri" or "entertainment" => "♪",
            "mua sắm" or "mua sam" or "shopping" => "◍",
            "khác" or "khac" or "other" => "○",
            _ => "●"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Kiem tra POI dang hien thi co trung voi POI dang phat khong.
/// values[0] = current narration poi id, values[1] = item poi id
/// </summary>
public class PoiMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        var currentPoiId = TryConvertToInt(values[0]);
        var itemPoiId = TryConvertToInt(values[1]);
        var isMatch = currentPoiId.HasValue && itemPoiId.HasValue && currentPoiId.Value == itemPoiId.Value;
        if (!isMatch)
            return false;

        if (values.Length >= 3)
        {
            return values[2] is bool isPlaying && isPlaying;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static int? TryConvertToInt(object? value)
    {
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}

/// <summary>
/// Hien icon dung/tiep tuc theo trang thai cua POI dang phat.
/// values[0] = current narration poi id, values[1] = item poi id, values[2] = is current narration paused
/// </summary>
public class StopResumeGlyphConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3)
            return "⏹";

        var currentPoiId = TryConvertToInt(values[0]);
        var itemPoiId = TryConvertToInt(values[1]);
        var isPaused = values[2] is bool paused && paused;

        var isCurrentItem = currentPoiId.HasValue && itemPoiId.HasValue && currentPoiId.Value == itemPoiId.Value;
        if (!isCurrentItem)
            return "⏹";

        return isPaused ? "▶" : "⏹";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static int? TryConvertToInt(object? value)
    {
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            short shortValue => shortValue,
            byte byteValue => byteValue,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}
