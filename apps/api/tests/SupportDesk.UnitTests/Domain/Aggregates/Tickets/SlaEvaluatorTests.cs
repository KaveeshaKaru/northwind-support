using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>
/// BR-8 at its boundaries. Every case uses an 8 h window that started at <see cref="Start"/>:
/// due at +8 h, at risk from +6 h (the last 25%).
/// </summary>
public class SlaEvaluatorTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;
    private static readonly DateTime Due = Start.AddHours(8);
    private static readonly DateTime AtRiskFrom = Start.AddHours(6);
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public void NoDueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, null, Start));

    [Fact]
    public void EarlyInTheWindow_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Due, AtRiskFrom, null, Start.AddHours(1)));

    [Fact]
    public void OneTickBeforeTheThreshold_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Due, AtRiskFrom, null, AtRiskFrom - Tick));

    [Fact]
    public void ExactlyAtTheThreshold_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Due, AtRiskFrom, null, AtRiskFrom));

    [Fact]
    public void ExactlyOnTheDueDate_Unresolved_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Due, AtRiskFrom, null, Due));

    [Fact]
    public void OneTickAfterTheDueDate_Unresolved_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Due, AtRiskFrom, null, Due + Tick));

    [Fact]
    public void ResolvedExactlyOnTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Due, AtRiskFrom, Due, Due.AddDays(1)));

    [Fact]
    public void ResolvedOneTickLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Due, AtRiskFrom, Due + Tick, Due.AddDays(1)));

    [Fact]
    public void ResolvedEarly_StaysMet_AfterTheDueDatePasses() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Due, AtRiskFrom, Start.AddHours(1), Due.AddDays(1)));

    [Fact]
    public void ADueDateWithoutAnAtRiskMoment_IsWithinSlaUntilDue()
    {
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Due, null, null, Due));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Due, null, null, Due + Tick));
    }
}
