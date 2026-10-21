using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets.Triage;

public class PriorityPolicyTests
{
    private static readonly Category Security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);
    private static readonly Category Billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(null)]
    public void ForcingCategory_IsCriticalWhateverWasRequested(TicketPriority? requested)
    {
        var decision = PriorityPolicy.ForNewTicket(requested, Security);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal("Category 'Security' is always treated as Critical", decision.Reason);
    }

    [Fact]
    public void RequestedPriority_IsUsed()
    {
        var decision = PriorityPolicy.ForNewTicket(TicketPriority.High, Billing);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Equal("Requested priority High", decision.Reason);
    }

    [Fact]
    public void NoRequest_DefaultsToMedium()
    {
        var decision = PriorityPolicy.ForNewTicket(null, Billing);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal("No priority requested; defaulted to Medium", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Next_StepsUpOneLevel(TicketPriority from, TicketPriority expected) =>
        Assert.Equal(expected, from.Next());

    [Fact]
    public void Next_OfCritical_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TicketPriority.Critical.Next());
}
