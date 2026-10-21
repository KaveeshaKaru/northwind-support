using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>What triage decided for a ticket, with a human-readable reason for each part.</summary>
public sealed record TriageDto(
    TicketPriority AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason)
{
    public static TriageDto From(TriageDecision decision) => new(
        decision.Priority,
        decision.PriorityReason,
        decision.Sla.Window.TotalHours,
        decision.Sla.Reason,
        decision.Assignment.Reason);
}
