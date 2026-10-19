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

    /// <summary>
    /// What escalating <paramref name="ticket"/> now would mean: one priority level up, a fresh
    /// SLA from this moment, and the current owner kept while still eligible.
    /// </summary>
    /// <exception cref="Exceptions.BusinessRuleViolationException">The ticket cannot be escalated.</exception>
    public TriageDecision ForEscalation(
        Ticket ticket,
        Category category,
        CustomerTier tier,
        IReadOnlyCollection<AgentWorkload> agents,
        DateTime nowUtc)
    {
        var priority = ticket.EscalatedPriority();

        return new TriageDecision(
            priority,
            $"Escalated from {ticket.Priority} to {priority}",
            sla.For(priority, tier, nowUtc),
            AgentSelector.Choose(category, agents, ticket.AssignedAgentId));
    }
}

/// <summary>Everything triage decided about a ticket, with a reason for each part.</summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    SlaTerms Sla,
    AssignmentDecision Assignment);
