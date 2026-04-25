namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Chấm điểm POI theo ngữ cảnh runtime để chọn POI phù hợp nhất.
/// </summary>
public static class PoiScoringService
{
    public const double CooldownPenaltyScore = 100000;

    public static double CalculateFinalPriority(PoiScoreContext context)
    {
        return (context.BasePriority * 100)
            + CalculateDistanceScore(context.DistanceMeters, context.ApproachRadiusMeters)
            + CalculateZoneScore(context.DistanceMeters, context.TriggerRadiusMeters, context.ApproachRadiusMeters)
            + context.TourOrderScore
            + CalculateContentReadyScore(context.HasOfflineAudio, context.HasOnlineAudio, context.HasTtsContent)
            + CalculatePopularityScore(context.ListenCountLast30Days)
            - CalculateCooldownPenalty(context.IsCooldownActive);
    }

    public static double CalculateDistanceScore(double distanceMeters, double approachRadiusMeters)
    {
        if (approachRadiusMeters <= 0 || distanceMeters > approachRadiusMeters)
            return 0;

        return 100 * (1 - (distanceMeters / approachRadiusMeters));
    }

    public static double CalculateZoneScore(double distanceMeters, double triggerRadiusMeters, double approachRadiusMeters)
    {
        if (triggerRadiusMeters > 0 && distanceMeters <= triggerRadiusMeters)
            return 80;

        if (approachRadiusMeters > 0 && distanceMeters <= approachRadiusMeters)
            return 30;

        return 0;
    }

    public static double CalculateContentReadyScore(bool hasOfflineAudio, bool hasOnlineAudio, bool hasTtsContent)
    {
        if (hasOfflineAudio)
            return 40;

        if (hasOnlineAudio)
            return 30;

        if (hasTtsContent)
            return 10;

        return 0;
    }

    public static double CalculatePopularityScore(int listenCountLast30Days)
    {
        if (listenCountLast30Days <= 0)
            return 0;

        return Math.Min(30, listenCountLast30Days / 10.0);
    }

    public static double CalculateCooldownPenalty(bool isCooldownActive)
    {
        return isCooldownActive ? CooldownPenaltyScore : 0;
    }
}

public sealed class PoiScoreContext
{
    public int BasePriority { get; init; }
    public double DistanceMeters { get; init; }
    public double TriggerRadiusMeters { get; init; }
    public double ApproachRadiusMeters { get; init; }
    public int TourOrderScore { get; init; }
    public bool HasOfflineAudio { get; init; }
    public bool HasOnlineAudio { get; init; }
    public bool HasTtsContent { get; init; }
    public int ListenCountLast30Days { get; init; }
    public bool IsCooldownActive { get; init; }
}
