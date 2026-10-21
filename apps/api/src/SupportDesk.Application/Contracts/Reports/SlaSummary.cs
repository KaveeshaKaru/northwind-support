namespace SupportDesk.Application.Contracts.Reports;

public enum SlaSummaryGroupBy
{
    Agent,
    Priority,
}

/// <summary>
/// Query string of GET /api/reports/sla-summary: tickets raised from <see cref="From"/>
/// (inclusive) to <see cref="To"/> (exclusive), in UTC.
/// </summary>
public sealed record SlaSummaryQuery
{
    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public SlaSummaryGroupBy? GroupBy { get; init; }
}

/// <summary>The report: the period it covers and one row per group.</summary>
public sealed record SlaSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    SlaSummaryGroupBy GroupBy,
    IReadOnlyList<SlaSummaryRowDto> Groups);

/// <summary>
/// One agent or priority. <see cref="BreachRate"/> is breached out of the tickets that have an
/// SLA (null when none do); <see cref="AverageResolutionHours"/> covers resolved tickets only.
/// </summary>
public sealed record SlaSummaryRowDto(
    string Group,
    int Total,
    int Breached,
    int AtRisk,
    int WithinSla,
    int Met,
    double? BreachRate,
    double? AverageResolutionHours)
{
    /// <summary>A row from the database's counts, with the breach rate worked out from them.</summary>
    public static SlaSummaryRowDto FromCounts(
        string group, int total, int breached, int atRisk, int withinSla, int met, double? averageResolutionHours)
    {
        var withSla = breached + atRisk + withinSla + met;

        return new SlaSummaryRowDto(
            group, total, breached, atRisk, withinSla, met,
            withSla == 0 ? null : Math.Round((double)breached / withSla, 3),
            averageResolutionHours is { } hours ? Math.Round(hours, 1) : null);
    }
}
