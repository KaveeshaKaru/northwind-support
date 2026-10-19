using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets.Triage;

/// <summary>
/// Decides a new ticket's priority (BR-1). The category's own flag, not its name, decides
/// whether it overrides what was requested.
/// </summary>
public static class PriorityPolicy
{
    public const TicketPriority Default = TicketPriority.Medium;

    public static PriorityDecision ForNewTicket(TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            return new PriorityDecision(
                TicketPriority.Critical,
                $"Category '{category.Name}' is always treated as Critical");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"Requested priority {priority}")
            : new PriorityDecision(Default, $"No priority requested; defaulted to {Default}");
    }
}

/// <summary>The priority a ticket gets, and why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);
