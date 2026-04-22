namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Quản lý đơn vị khoảng cách hiển thị trong app.
/// </summary>
public static class DistanceUnitService
{
    public static string FormatFromMeters(double meters)
    {
        var safeMeters = Math.Max(0, meters);

        if (safeMeters < 1000d)
            return $"{Math.Round(safeMeters):0} m";

        var km = safeMeters / 1000d;
        return $"{km:0.#} km";
    }

    public static string FormatAsKilometers(double meters)
    {
        return FormatFromMeters(meters);
    }
}
