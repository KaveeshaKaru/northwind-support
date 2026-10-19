using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Every agent with what assignment needs to know, in one round trip: the open-ticket count is
/// a correlated subquery and the specializations come back in the same statement, so there is
/// no query per agent.
/// </summary>
public sealed class AgentWorkloads(SupportDbContext db) : IAgentWorkloads
{
    public async Task<IReadOnlyList<AgentWorkload>> GetAllAsync(CancellationToken ct)
    {
        var rows = await db.Set<Agent>()
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                OpenTickets = db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                CategoryIds = a.Specializations.Select(s => s.CategoryId).ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new AgentWorkload(
                r.Id, r.FullName, r.IsActive, r.MaxOpenTickets, r.OpenTickets, r.CategoryIds.ToHashSet()))
            .ToList();
    }
}
