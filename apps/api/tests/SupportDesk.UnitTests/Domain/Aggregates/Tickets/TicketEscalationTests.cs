using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;
using static SupportDesk.UnitTests.TestDoubles.SeedDesk;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>BR-7: escalation goes through the same triage rules as a new ticket.</summary>
public class TicketEscalationTests
{
    private static readonly DateTime TwoPm = new(2026, 3, 2, 14, 0, 0, DateTimeKind.Utc);
    private readonly TicketTriage _triage = new(TestSla.Policy());

    private TicketEscalation Escalate(
        Ticket ticket,
        Category category,
        IReadOnlyCollection<AgentWorkload>? agents = null,
        CustomerTier tier = CustomerTier.Standard)
    {
        var decision = _triage.ForEscalation(ticket, category, tier, agents ?? Agents, TwoPm);
        return ticket.Escalate(decision, "  Customer's site is down.  ", " alex.lead ", TwoPm);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesPriorityOneLevel(TicketPriority from, TicketPriority to)
    {
        var ticket = new TicketBuilder().WithPriority(from).AssignedTo(Alex.AgentId).Build();

        Escalate(ticket, General);

        Assert.Equal(to, ticket.Priority);
    }

    [Fact]
    public void Escalate_RecomputesTheDueDateFromNow_NotFromCreation()
    {
        // The assignment's example: a High ticket escalated at 14:00 for a Standard customer.
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).AssignedTo(Alex.AgentId).Build();

        Escalate(ticket, General);

        Assert.Equal(TwoPm.AddHours(4), ticket.DueAtUtc);
        Assert.Equal(TwoPm.AddHours(3), ticket.AtRiskFromUtc);
    }

    [Fact]
    public void Escalate_KeepsTheAgent_WhenStillEligible()
    {
        // Sara is less busy, but Alex already owns it and can still take it.
        var ticket = new TicketBuilder().AssignedTo(Alex.AgentId).Build();

        Escalate(ticket, General);

        Assert.Equal(Alex.AgentId, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_KeepsAFullAgent_OnTheTicketTheyAlreadyHold()
    {
        var ticket = new TicketBuilder().AssignedTo(Marcus.AgentId).Build();

        Escalate(ticket, Billing);

        Assert.Equal(Marcus.AgentId, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_Reassigns_WhenTheAgentIsNoLongerEligible()
    {
        var ticket = new TicketBuilder().AssignedTo(Yuki.AgentId).Build();

        Escalate(ticket, Technical);

        Assert.Equal(Sara.AgentId, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_WithNobodyEligible_LeavesTheTicketUnassigned()
    {
        var ticket = new TicketBuilder().AssignedTo(Yuki.AgentId).Build();

        Escalate(ticket, Security, agents: [Marcus, Yuki]);

        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_RecordsWhatChanged_WhoAndWhy()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(Yuki.AgentId).Build();
        var dueBefore = ticket.DueAtUtc;

        var escalation = Escalate(ticket, Technical);

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(Yuki.AgentId, escalation.FromAgentId);
        Assert.Equal(Sara.AgentId, escalation.ToAgentId);
        Assert.Equal(dueBefore, escalation.FromDueAtUtc);
        Assert.Equal(TwoPm.AddHours(8), escalation.ToDueAtUtc);
        Assert.Equal("Customer's site is down.", escalation.Reason);
        Assert.Equal("alex.lead", escalation.EscalatedBy);
        Assert.Equal(TwoPm, escalation.EscalatedAtUtc);
        Assert.Same(escalation, Assert.Single(ticket.Escalations));
    }

    [Fact]
    public void Critical_CannotBeEscalated()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0006").WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket, General));

        Assert.Equal("Ticket TCK-0006 is already Critical and cannot be escalated further.", exception.Message);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void ResolvedOrClosed_CannotBeEscalated(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithReference("TCK-0025").WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket, General));

        Assert.Equal($"Ticket TCK-0025 is {status} and cannot be escalated.", exception.Message);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }
}
