using SupportDesk.Domain.Aggregates.Tickets.Triage;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The support team as ticket assignment sees it: every agent with their open-ticket count,
/// limit and specializations, read in one query.
/// </summary>
public interface IAgentWorkloads
{
    Task<IReadOnlyList<AgentWorkload>> GetAllAsync(CancellationToken ct);
}
