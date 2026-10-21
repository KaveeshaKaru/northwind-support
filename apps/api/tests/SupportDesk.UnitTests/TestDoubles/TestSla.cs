using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA settings from the assignment (and appsettings.json), so tests state the same rules
/// the application runs with.
/// </summary>
public static class TestSla
{
    public static SlaSettings Settings(double criticalBaseHours = 4) => new()
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = criticalBaseHours
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    public static SlaPolicy Policy(double criticalBaseHours = 4) => new(Settings(criticalBaseHours));
}
