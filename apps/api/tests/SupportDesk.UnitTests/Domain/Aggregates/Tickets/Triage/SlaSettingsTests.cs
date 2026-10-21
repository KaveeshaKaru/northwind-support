using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets.Triage;

public class SlaSettingsTests
{
    [Fact]
    public void TheAssignmentsSettings_AreValid() =>
        Assert.Empty(TestSla.Settings().Validate());

    [Fact]
    public void AMissingPriority_IsReported()
    {
        var settings = TestSla.Settings();
        settings.BaseWindowHours.Remove(TicketPriority.High);

        Assert.Contains(settings.Validate(), error => error.Contains("High"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void AMultiplierOutsideZeroToOne_IsReported(double multiplier)
    {
        var settings = TestSla.Settings();
        settings.PremiumCustomerMultiplier = multiplier;

        Assert.NotEmpty(settings.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void AThresholdOutsideZeroToOneHundred_IsReported(double percent)
    {
        var settings = TestSla.Settings();
        settings.AtRiskThresholdPercent = percent;

        Assert.NotEmpty(settings.Validate());
    }

    [Fact]
    public void ANonPositiveFloor_IsReported()
    {
        var settings = TestSla.Settings();
        settings.MinimumWindowHours = 0;

        Assert.NotEmpty(settings.Validate());
    }
}
