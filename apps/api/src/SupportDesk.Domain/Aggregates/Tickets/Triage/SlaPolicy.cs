using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets.Triage;

/// <summary>
/// Works out a ticket's SLA (BR-2, BR-3): the base window for its priority, adjusted for a
/// Premium customer, never below the floor. Used when a ticket is raised and when it is
/// escalated, so both get the same answer.
/// </summary>
public sealed class SlaPolicy(SlaSettings settings)
{
    /// <param name="priority">The priority the ticket will have.</param>
    /// <param name="tier">The customer's tier.</param>
    /// <param name="startUtc">When the clock starts: creation, or the moment of escalation.</param>
    public SlaTerms For(TicketPriority priority, CustomerTier tier, DateTime startUtc)
    {
        var baseHours = settings.BaseWindowHours[priority];
        var reason = $"{priority} base window {Hours(baseHours)}";

        var hours = baseHours;

        if (tier == CustomerTier.Premium)
        {
            hours *= settings.PremiumCustomerMultiplier;
            reason += settings.PremiumCustomerMultiplier == 0.5
                ? ", halved for a Premium customer"
                : $", multiplied by {settings.PremiumCustomerMultiplier.ToString(CultureInfo.InvariantCulture)} for a Premium customer";
        }

        if (hours < settings.MinimumWindowHours)
        {
            hours = settings.MinimumWindowHours;
            reason += $", raised to the {Hours(hours)} minimum";
        }

        var window = TimeSpan.FromHours(hours);
        var dueAtUtc = startUtc + window;
        var atRiskFromUtc = dueAtUtc - window * (settings.AtRiskThresholdPercent / 100);

        return new SlaTerms(window, dueAtUtc, atRiskFromUtc, reason);
    }

    private static string Hours(double hours) => $"{hours.ToString("0.##", CultureInfo.InvariantCulture)}h";
}

/// <summary>
/// The response commitment for a ticket: how long the team has, when that runs out, and from
/// when the ticket counts as at risk.
/// </summary>
public sealed record SlaTerms(TimeSpan Window, DateTime DueAtUtc, DateTime AtRiskFromUtc, string Reason);
