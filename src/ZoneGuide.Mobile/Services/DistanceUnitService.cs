namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Quản lý đơn vị khoảng cách hiển thị trong app.
/// </summary>
public static class DistanceUnitService
{
    private static string _preferredUnit = "m";

    public static string PreferredUnit => _preferredUnit;

    public static void SetPreferredUnit(string? unit)
    {
        _preferredUnit = NormalizeUnit(unit);
    }

    public static string NormalizeUnit(string? unit)
    {
        return string.Equals(unit, "km", StringComparison.OrdinalIgnoreCase) ? "km" : "m";
    }

    public static string FormatFromMeters(double meters)
    {
        var safeMeters = Math.Max(0, meters);

        if (string.Equals(_preferredUnit, "km", StringComparison.OrdinalIgnoreCase))
        {
            var km = safeMeters / 1000d;
            return km >= 10
                ? $"{km:0} km"
                : $"{km:0.#} km";
        }

        return $"{Math.Round(safeMeters):0} m";
    }
}
