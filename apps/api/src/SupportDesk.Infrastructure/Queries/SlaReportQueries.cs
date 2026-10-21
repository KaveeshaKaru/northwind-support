using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// The SLA summary as one GROUP BY in the database: each ticket's status comes from the same
/// rule as the ticket list (<see cref="SlaStatusSql"/>), then is counted per group.
/// </summary>
public sealed class SlaReportQueries(SupportDbContext db, IClock clock) : ISlaReportQueries
{
    private const string Unassigned = "Unassigned";

    public async Task<IReadOnlyList<SlaSummaryRowDto>> GetSlaSummaryAsync(
        DateTime fromUtc, DateTime toUtc, SlaSummaryGroupBy groupBy, CancellationToken ct)
    {
        var tickets =
            from ticket in db.Set<Ticket>().AsNoTracking()
            where ticket.CreatedAtUtc >= fromUtc && ticket.CreatedAtUtc < toUtc
            from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
            select new TicketWithAgent { Ticket = ticket, Agent = agent };

        var groups = await tickets
            .Select(ToRow(groupBy, clock.UtcNow))
            .GroupBy(row => new { row.GroupId, row.GroupName })
            .Select(group => new
            {
                group.Key.GroupId,
                group.Key.GroupName,
                Total = group.Count(),
                Breached = group.Count(row => row.Status == SlaStatus.Breached),
                AtRisk = group.Count(row => row.Status == SlaStatus.AtRisk),
                WithinSla = group.Count(row => row.Status == SlaStatus.WithinSla),
                Met = group.Count(row => row.Status == SlaStatus.Met),
                AverageResolutionHours = group.Average(row => row.ResolutionHours),
            })
            .ToListAsync(ct);

        var rows = groups.Select(g => new
        {
            g.GroupId,
            Row = SlaSummaryRowDto.FromCounts(
                Label(groupBy, g.GroupId, g.GroupName), g.Total, g.Breached, g.AtRisk, g.WithinSla, g.Met,
                g.AverageResolutionHours),
        });

        // Most urgent priority first; agents by name, with unassigned tickets last.
        return (groupBy == SlaSummaryGroupBy.Priority
                ? rows.OrderByDescending(r => r.GroupId)
                : rows.OrderBy(r => r.GroupId is null).ThenBy(r => r.Row.Group, StringComparer.Ordinal))
            .Select(r => r.Row)
            .ToList();
    }

    /// <summary>
    /// One row per ticket: its group, its SLA status at <paramref name="nowUtc"/> and how long it
    /// took to resolve. Built as an expression so the status is the shared rule, inlined.
    /// </summary>
    private static Expression<Func<TicketWithAgent, ReportRow>> ToRow(SlaSummaryGroupBy groupBy, DateTime nowUtc)
    {
        Expression<Func<TicketWithAgent, SlaStatus, ReportRow>> byAgent = (x, status) => new ReportRow
        {
            GroupId = x.Ticket.AssignedAgentId,
            GroupName = x.Agent == null ? null : x.Agent.FullName,
            Status = status,
            ResolutionHours = SqlDate.HoursBetween(x.Ticket.CreatedAtUtc, x.Ticket.ResolvedAtUtc),
        };

        Expression<Func<TicketWithAgent, SlaStatus, ReportRow>> byPriority = (x, status) => new ReportRow
        {
            GroupId = (int)x.Ticket.Priority,
            GroupName = null,
            Status = status,
            ResolutionHours = SqlDate.HoursBetween(x.Ticket.CreatedAtUtc, x.Ticket.ResolvedAtUtc),
        };

        var template = groupBy == SlaSummaryGroupBy.Priority ? byPriority : byAgent;
        var row = template.Parameters[0];
        var status = SlaStatusSql.StatusOf(Expression.Property(row, nameof(TicketWithAgent.Ticket)), nowUtc);

        var body = new ParameterReplacer(new Dictionary<ParameterExpression, Expression>
        {
            [template.Parameters[1]] = status
        }).Visit(template.Body);

        return Expression.Lambda<Func<TicketWithAgent, ReportRow>>(body, row);
    }

    private static string Label(SlaSummaryGroupBy groupBy, int? groupId, string? groupName) =>
        groupBy == SlaSummaryGroupBy.Priority
            ? ((TicketPriority)groupId!.Value).ToString()
            : groupName ?? Unassigned;

    private sealed class TicketWithAgent
    {
        public required Ticket Ticket { get; init; }

        public Agent? Agent { get; init; }
    }

    private sealed class ReportRow
    {
        public int? GroupId { get; init; }

        public string? GroupName { get; init; }

        public SlaStatus Status { get; init; }

        public double? ResolutionHours { get; init; }
    }
}
