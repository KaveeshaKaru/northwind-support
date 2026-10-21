using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets.Triage;

public class SlaPolicyTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;

    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    public void Window_ForEachPriorityAndTier(TicketPriority priority, CustomerTier tier, double expectedHours)
    {
        var terms = TestSla.Policy().For(priority, tier, Start);

        Assert.Equal(TimeSpan.FromHours(expectedHours), terms.Window);
        Assert.Equal(Start.AddHours(expectedHours), terms.DueAtUtc);
    }

    [Fact]
    public void Window_ForPremium_NeverBelowTheFloor()
    {
        // The assignment's hypothetical: a 1 h base window, halved, would be 30 minutes.
        var terms = TestSla.Policy(criticalBaseHours: 1).For(TicketPriority.Critical, CustomerTier.Premium, Start);

        Assert.Equal(TimeSpan.FromHours(1), terms.Window);
        Assert.Contains("1h minimum", terms.Reason);
    }

    [Fact]
    public void AtRiskFrom_IsTheLastQuarterOfTheWindow()
    {
        var start = new DateTime(2026, 3, 2, 14, 0, 0, DateTimeKind.Utc);

        var terms = TestSla.Policy().For(TicketPriority.High, CustomerTier.Standard, start);

        Assert.Equal(start.AddHours(8), terms.DueAtUtc);
        Assert.Equal(start.AddHours(6), terms.AtRiskFromUtc);
    }

    [Fact]
    public void Reason_ForStandard_NamesTheBaseWindow() =>
        Assert.Equal(
            "High base window 8h",
            TestSla.Policy().For(TicketPriority.High, CustomerTier.Standard, Start).Reason);

    [Fact]
    public void Reason_ForPremium_SaysTheWindowWasHalved() =>
        Assert.Equal(
            "Critical base window 4h, halved for a Premium customer",
            TestSla.Policy().For(TicketPriority.Critical, CustomerTier.Premium, Start).Reason);
}
