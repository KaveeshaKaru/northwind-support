using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets.Triage;

/// <summary>
/// Decides a ticket's priority, SLA and owner in one place, from the three policies. Raising a
/// ticket and escalating one both come through here, so they cannot apply the rules differently.
/// </summary>
public sealed class TicketTriage(SlaPolicy sla)
{
    public TriageDecision ForNewTicket(
        TicketPriority? requestedPriority,
        Category category,
        CustomerTier tier,
        IReadOnlyCollection<AgentWorkload> agents,
        DateTime nowUtc)
    {
        var priority = PriorityPolicy.ForNewTicket(requestedPriority, category);

        return new TriageDecision(
            priority.Priority,
            priority.Reason,
            sla.For(priority.Priority, tier, nowUtc),
            AgentSelector.Choose(category, agents));
    }
}

/// <summary>Everything triage decided about a ticket, with a reason for each part.</summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    SlaTerms Sla,
    AssignmentDecision Assignment);
