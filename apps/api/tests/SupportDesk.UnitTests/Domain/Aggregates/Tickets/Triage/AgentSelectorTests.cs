using SupportDesk.Domain.Aggregates.Tickets.Triage;
using static SupportDesk.UnitTests.TestDoubles.SeedDesk;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets.Triage;

public class AgentSelectorTests
{
    [Fact]
    public void FewestOpenTickets_Wins()
    {
        var decision = AgentSelector.Choose(Technical, [Agent(1, "A", open: 4, max: 10), Agent(2, "B", open: 1, max: 10)]);

        Assert.Equal(2, decision.AgentId);
        Assert.Equal("Fewest open tickets among available agents", decision.Reason);
    }

    [Fact]
    public void SpecialistCategory_OnlyGoesToSpecialists()
    {
        // Sara has the fewest open tickets of anyone, but is not an Outage specialist.
        var decision = AgentSelector.Choose(Outage, Agents);

        Assert.Equal(Ben.AgentId, decision.AgentId);
        Assert.Equal("Fewest open tickets among Outage specialists", decision.Reason);
    }

    [Fact]
    public void NonSpecialistCategory_GoesToAnyActiveAgentWithCapacity()
    {
        var decision = AgentSelector.Choose(Technical, Agents);

        Assert.Equal(Sara.AgentId, decision.AgentId);
    }

    [Fact]
    public void AgentAtTheirLimit_IsExcluded()
    {
        var decision = AgentSelector.Choose(General, [Agent(1, "Full", open: 6, max: 6), Agent(2, "Busy", open: 9, max: 10)]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void InactiveAgent_IsExcluded()
    {
        // Yuki has no open tickets at all, which would win if inactive agents counted.
        var decision = AgentSelector.Choose(Technical, [Yuki, Alex]);

        Assert.Equal(Alex.AgentId, decision.AgentId);
    }

    [Fact]
    public void Tie_GoesToTheLowestId_WhateverTheOrder()
    {
        var first = AgentSelector.Choose(General, [Agent(7, "Late", open: 2, max: 10), Agent(3, "Early", open: 2, max: 10)]);
        var second = AgentSelector.Choose(General, [Agent(3, "Early", open: 2, max: 10), Agent(7, "Late", open: 2, max: 10)]);

        Assert.Equal(3, first.AgentId);
        Assert.Equal(3, second.AgentId);
    }

    [Fact]
    public void NobodyEligible_ForASpecialistCategory_IsUnassignedWithAReason()
    {
        var decision = AgentSelector.Choose(Security, [Marcus, Yuki, Sara]);

        Assert.Null(decision.AgentId);
        Assert.Equal("No available agent specialised in Security", decision.Reason);
    }

    [Fact]
    public void NobodyEligible_ForAnOpenCategory_IsUnassignedWithAReason()
    {
        var decision = AgentSelector.Choose(General, [Marcus with { SpecializationCategoryIds = new HashSet<int>() }]);

        Assert.Null(decision.AgentId);
        Assert.Equal("No available agent with capacity", decision.Reason);
    }

    [Fact]
    public void CurrentAgent_IsKept_WhenStillEligible()
    {
        // Sara would be picked as the least busy, but Alex already owns the ticket.
        var decision = AgentSelector.Choose(General, Agents, currentAgentId: Alex.AgentId);

        Assert.Equal(Alex.AgentId, decision.AgentId);
        Assert.Equal("Kept Alex Turner: still eligible", decision.Reason);
    }

    [Fact]
    public void CurrentAgent_AtTheirLimit_KeepsTheTicketTheyAlreadyHold()
    {
        // Marcus is at 6/6, but one of those six is this ticket: keeping it adds no work.
        var decision = AgentSelector.Choose(Billing, Agents, currentAgentId: Marcus.AgentId);

        Assert.Equal(Marcus.AgentId, decision.AgentId);
    }

    [Fact]
    public void CurrentAgent_NoLongerEligible_IsReplaced()
    {
        var decision = AgentSelector.Choose(Technical, Agents, currentAgentId: Yuki.AgentId);

        Assert.Equal(Sara.AgentId, decision.AgentId);
    }
}
