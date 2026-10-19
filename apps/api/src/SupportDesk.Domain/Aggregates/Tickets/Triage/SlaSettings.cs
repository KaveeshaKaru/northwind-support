namespace SupportDesk.Domain.Aggregates.Tickets.Triage;

/// <summary>
/// The numbers behind the SLA rules. Bound from the <c>Sla</c> configuration section, so the
/// windows, the premium multiplier, the floor and the at-risk threshold change without a deploy.
/// </summary>
public sealed class SlaSettings
{
    public const string SectionName = "Sla";

    /// <summary>The response window for each priority, before any customer adjustment.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    /// <summary>Applied to a Premium customer's window; 0.5 halves it.</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>No window, after adjustment, is ever shorter than this.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>A ticket is at risk once this share of its window, or less, remains.</summary>
    public double AtRiskThresholdPercent { get; set; }

    /// <summary>What is wrong with these settings; empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                errors.Add($"Sla:BaseWindowHours:{priority} must be a positive number of hours.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            errors.Add("Sla:PremiumCustomerMultiplier must be greater than 0 and at most 1.");
        }

        if (MinimumWindowHours <= 0)
        {
            errors.Add("Sla:MinimumWindowHours must be positive.");
        }

        if (AtRiskThresholdPercent is < 0 or >= 100)
        {
            errors.Add("Sla:AtRiskThresholdPercent must be from 0 up to (not including) 100.");
        }

        return errors;
    }
}
