using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

/// <summary>The SLA summary (2.5). The query has already been validated.</summary>
public sealed class GetSlaSummaryQueryHandler(ISlaReportQueries reports)
{
    public async Task<SlaSummaryDto> HandleAsync(SlaSummaryQuery query, CancellationToken ct)
    {
        var fromUtc = AsUtc(query.From!.Value);
        var toUtc = AsUtc(query.To!.Value);
        var groupBy = query.GroupBy!.Value;

        var groups = await reports.GetSlaSummaryAsync(fromUtc, toUtc, groupBy, ct);

        return new SlaSummaryDto(fromUtc, toUtc, groupBy, groups);
    }

    /// <summary>A date without a zone ("2026-10-01") is read as UTC, like every date in this API.</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
