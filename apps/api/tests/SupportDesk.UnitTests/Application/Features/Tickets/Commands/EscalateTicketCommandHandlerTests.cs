using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("  Customer's site is down.  ");
    private const string SignedInAgent = "sara.lindqvist@northwind-support.example";

    [Fact]
    public async Task UnknownTicket_IsNotFound()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(404, Request, SignedInAgent, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Escalate_SavesOnce_AndReturnsTheDecisionAndTheRecordedEscalation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.High).AssignedTo(SeedDesk.Alex.AgentId).Build());

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        var escalation = Assert.Single(ticket.Escalations);
        Assert.Equal("Customer's site is down.", escalation.Reason);
        Assert.Equal(SignedInAgent, escalation.EscalatedBy);
        Assert.Equal("Escalated from High to Critical", result.Triage?.PriorityReason);
        Assert.NotNull(result.Escalation);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task CriticalTicket_IsRejected_AndNothingIsSaved()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
