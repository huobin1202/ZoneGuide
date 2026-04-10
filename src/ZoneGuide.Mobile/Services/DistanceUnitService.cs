namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Quản lý đơn vị khoảng cách hiển thị trong app.
/// </summary>
public static class DistanceUnitService
{
    private static string _preferredUnit = "km";

    public static string PreferredUnit => _preferredUnit;

    public static void SetPreferredUnit(string? unit)
    {
        _preferredUnit = NormalizeUnit(unit);
    }

    public static string NormalizeUnit(string? unit)
    {
        return string.Equals(unit, "m", StringComparison.OrdinalIgnoreCase) ? "m" : "km";
    }

    public static string FormatFromMeters(double meters, string? unitOverride = null)
    {
        var safeMeters = Math.Max(0, meters);
        var unit = string.IsNullOrWhiteSpace(unitOverride)
            ? _preferredUnit
            : NormalizeUnit(unitOverride);

        if (string.Equals(unit, "m", StringComparison.OrdinalIgnoreCase))
            return $"{Math.Round(safeMeters):0} m";

        var km = safeMeters / 1000d;

        if (km < 1)
            return $"{km:0.##} km";

        return $"{km:0.#} km";
    }

    public static string FormatAsKilometers(double meters)
    {
        return FormatFromMeters(meters, "km");
    }
}
