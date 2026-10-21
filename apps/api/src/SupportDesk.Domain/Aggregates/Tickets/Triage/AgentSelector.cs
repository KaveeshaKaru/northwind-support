using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets.Triage;

/// <summary>
/// Picks who owns a ticket (BR-4, BR-5). An agent is eligible when active, specialised in the
/// category if it requires a specialist, and strictly below their open-ticket limit. The least
/// busy eligible agent wins; ties go to the lowest id, so the same desk always gives the same
/// answer. Nobody eligible is a normal outcome, not an error.
/// </summary>
public static class AgentSelector
{
    /// <param name="category">The ticket's category.</param>
    /// <param name="agents">Every agent, with their current workload.</param>
    /// <param name="currentAgentId">
    /// Who owns the ticket now, when re-evaluating an existing ticket. They keep it while still
    /// eligible, and the ticket itself does not count against their limit.
    /// </param>
    public static AssignmentDecision Choose(
        Category category,
        IReadOnlyCollection<AgentWorkload> agents,
        int? currentAgentId = null)
    {
        var eligible = agents.Where(agent => IsEligible(agent, category, currentAgentId)).ToList();

        if (eligible.FirstOrDefault(agent => agent.AgentId == currentAgentId) is { } current)
        {
            return new AssignmentDecision(current.AgentId, $"Kept {current.FullName}: still eligible");
        }

        var chosen = eligible
            .OrderBy(agent => agent.OpenTickets)
            .ThenBy(agent => agent.AgentId)
            .FirstOrDefault();

        if (chosen is null)
        {
            return new AssignmentDecision(null, category.RequiresSpecialist
                ? $"No available agent specialised in {category.Name}"
                : "No available agent with capacity");
        }

        return new AssignmentDecision(chosen.AgentId, category.RequiresSpecialist
            ? $"Fewest open tickets among {category.Name} specialists"
            : "Fewest open tickets among available agents");
    }

    private static bool IsEligible(AgentWorkload agent, Category category, int? currentAgentId)
    {
        // The current owner's count already includes this ticket.
        var openTickets = agent.AgentId == currentAgentId ? agent.OpenTickets - 1 : agent.OpenTickets;

        return agent.IsActive
            && (!category.RequiresSpecialist || agent.SpecializationCategoryIds.Contains(category.Id))
            && openTickets < agent.MaxOpenTickets;
    }
}

/// <summary>An agent as assignment sees them: availability, capacity and qualifications.</summary>
public sealed record AgentWorkload(
    int AgentId,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTickets,
    IReadOnlySet<int> SpecializationCategoryIds);

/// <summary>Who gets the ticket - possibly nobody - and why.</summary>
public sealed record AssignmentDecision(int? AgentId, string Reason);
