namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How urgent a ticket is. The numeric values are ordered, lowest urgency first, and are
/// persisted as integers - do not renumber them.
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public static class TicketPriorities
{
    /// <summary>The next level up: Low, Medium, High, Critical.</summary>
    /// <exception cref="InvalidOperationException">There is nothing above Critical.</exception>
    public static TicketPriority Next(this TicketPriority priority) =>
        priority == TicketPriority.Critical
            ? throw new InvalidOperationException("Critical is the highest priority.")
            : priority + 1;
}
