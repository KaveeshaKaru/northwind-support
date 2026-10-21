using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.UnitTests.TestDoubles;
using static SupportDesk.UnitTests.TestDoubles.SeedDesk;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets.Triage;

/// <summary>The worked examples from section 2.2 of the assignment, against the seeded desk.</summary>
public class TicketTriageTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private readonly TicketTriage _triage = new(TestSla.Policy());

    public static TheoryData<string, CustomerTier, Category, TicketPriority?, TicketPriority, double, string> WorkedExamples => new()
    {
        { "Adventure Works", CustomerTier.Premium, Outage, TicketPriority.Medium, TicketPriority.Critical, 2, "Ben Osei" },
        { "Contoso", CustomerTier.Premium, Security, TicketPriority.Low, TicketPriority.Critical, 2, "Priya Nair" },
        { "Fabrikam", CustomerTier.Standard, Billing, TicketPriority.High, TicketPriority.High, 8, "Sara Lindqvist" },
        { "Tailspin", CustomerTier.Standard, Technical, null, TicketPriority.Medium, 24, "Sara Lindqvist" },
        { "Wide World", CustomerTier.Premium, General, TicketPriority.Low, TicketPriority.Low, 36, "Sara Lindqvist" },
    };

    [Theory]
    [MemberData(nameof(WorkedExamples))]
    public void NewTicket_MatchesTheWorkedExample(
        string customer, CustomerTier tier, Category category, TicketPriority? requested,
        TicketPriority expectedPriority, double expectedWindowHours, string expectedAgent)
    {
        _ = customer; // Named in the data so a failing case says which example it was.

        var decision = _triage.ForNewTicket(requested, category, tier, Agents, Now);

        Assert.Equal(expectedPriority, decision.Priority);
        Assert.Equal(TimeSpan.FromHours(expectedWindowHours), decision.Sla.Window);
        Assert.Equal(Now.AddHours(expectedWindowHours), decision.Sla.DueAtUtc);
        Assert.Equal(Agents.Single(a => a.FullName == expectedAgent).AgentId, decision.Assignment.AgentId);
    }

    [Fact]
    public void NewTicket_CarriesAReasonForEachPart()
    {
        var decision = _triage.ForNewTicket(TicketPriority.Low, Security, CustomerTier.Premium, Agents, Now);

        Assert.Equal("Category 'Security' is always treated as Critical", decision.PriorityReason);
        Assert.Equal("Critical base window 4h, halved for a Premium customer", decision.Sla.Reason);
        Assert.Equal("Fewest open tickets among Security specialists", decision.Assignment.Reason);
    }

    [Fact]
    public void NewTicket_WithNobodyEligible_IsUnassignedNotAnError()
    {
        var decision = _triage.ForNewTicket(null, Security, CustomerTier.Standard, [Marcus, Yuki], Now);

        Assert.Null(decision.Assignment.AgentId);
        Assert.Equal("No available agent specialised in Security", decision.Assignment.Reason);
    }
}
