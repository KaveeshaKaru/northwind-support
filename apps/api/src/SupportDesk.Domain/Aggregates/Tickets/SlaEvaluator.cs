using System.Linq.Expressions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its SLA (BR-8). SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
/// <remarks>
/// The rule is written once, as an expression, so that the same definition is compiled for C#
/// and translated to SQL when the ticket list is filtered by SLA status.
/// </remarks>
public static class SlaEvaluator
{
    /// <summary>
    /// The rule, over (due, at risk from, resolved, now). Only comparisons, so any database
    /// can run it.
    /// </summary>
    public static readonly Expression<Func<DateTime?, DateTime?, DateTime?, DateTime, SlaStatus>> Rule =
        (dueAtUtc, atRiskFromUtc, resolvedAtUtc, nowUtc) =>
            dueAtUtc == null ? SlaStatus.NotApplicable
            : resolvedAtUtc != null ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
            : nowUtc > dueAtUtc ? SlaStatus.Breached
            : nowUtc >= atRiskFromUtc ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;

    private static readonly Func<DateTime?, DateTime?, DateTime?, DateTime, SlaStatus> CompiledRule = Rule.Compile();

    /// <param name="dueAtUtc">When the SLA runs out; null when no SLA applies.</param>
    /// <param name="atRiskFromUtc">When the ticket enters the last part of its window.</param>
    /// <param name="resolvedAtUtc">When the ticket was resolved; null while it is open.</param>
    /// <param name="nowUtc">The current time, injected so tests never depend on the clock.</param>
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? atRiskFromUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        CompiledRule(dueAtUtc, atRiskFromUtc, resolvedAtUtc, nowUtc);
}
