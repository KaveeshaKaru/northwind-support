using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.Domain.Common;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The seeded support desk from the assignment, as triage sees it: the five categories and the
/// six agents with their open tickets, limits and specializations.
/// </summary>
public static class SeedDesk
{
    public static readonly Category General = CategoryWithId(1, "General", requiresSpecialist: false, forcesCritical: false);
    public static readonly Category Billing = CategoryWithId(2, "Billing", requiresSpecialist: true, forcesCritical: false);
    public static readonly Category Technical = CategoryWithId(3, "Technical", requiresSpecialist: false, forcesCritical: false);
    public static readonly Category Outage = CategoryWithId(4, "Outage", requiresSpecialist: true, forcesCritical: true);
    public static readonly Category Security = CategoryWithId(5, "Security", requiresSpecialist: true, forcesCritical: true);

    public static readonly AgentWorkload Alex = Agent(1, "Alex Turner", open: 5, max: 10, General, Technical);
    public static readonly AgentWorkload Ben = Agent(2, "Ben Osei", open: 3, max: 8, Technical, Outage);
    public static readonly AgentWorkload Priya = Agent(3, "Priya Nair", open: 4, max: 8, Outage, Security);
    public static readonly AgentWorkload Sara = Agent(4, "Sara Lindqvist", open: 2, max: 6, General, Billing);
    public static readonly AgentWorkload Marcus = Agent(5, "Marcus Doyle", open: 6, max: 6, Billing, Security);
    public static readonly AgentWorkload Yuki = Agent(6, "Yuki Tanaka", open: 0, max: 8, Technical) with { IsActive = false };

    public static IReadOnlyList<AgentWorkload> Agents { get; } = [Alex, Ben, Priya, Sara, Marcus, Yuki];

    public static AgentWorkload Agent(int id, string name, int open, int max, params Category[] specializations) =>
        new(id, name, IsActive: true, MaxOpenTickets: max, OpenTickets: open,
            specializations.Select(c => c.Id).ToHashSet());

    /// <summary>
    /// Categories get their id from the database; tests need distinct ids without one, so this
    /// sets it the way EF Core would.
    /// </summary>
    public static Category CategoryWithId(int id, string name, bool requiresSpecialist, bool forcesCritical)
    {
        var category = new Category(name, requiresSpecialist, forcesCritical);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, id);
        return category;
    }
}
