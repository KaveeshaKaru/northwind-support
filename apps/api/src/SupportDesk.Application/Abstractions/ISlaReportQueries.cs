using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Abstractions;

/// <summary>Reporting reads: aggregated in the database, never by loading tickets.</summary>
public interface ISlaReportQueries
{
    /// <summary>One row per group, for tickets raised in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<SlaSummaryRowDto>> GetSlaSummaryAsync(
        DateTime fromUtc, DateTime toUtc, SlaSummaryGroupBy groupBy, CancellationToken ct);
}
