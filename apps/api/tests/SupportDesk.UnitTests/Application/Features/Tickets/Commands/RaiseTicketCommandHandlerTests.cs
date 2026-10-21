using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_AppliesTriage_ToTheSavedTicket()
    {
        // Tailspin-style example: Standard customer, Technical, no priority -> Medium, 24 h, Sara.
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        var created = context.AddedTicket!;
        Assert.Equal(FixedClock.DefaultNow.AddHours(24), created.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow.AddHours(18), created.AtRiskFromUtc);
        Assert.Equal(SeedDesk.Sara.AgentId, created.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheTriageReasons()
    {
        var context = new TicketCommandTestContext { Category = SeedDesk.Security };

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(result.Triage);
        Assert.Equal(TicketPriority.Critical, result.Triage.AppliedPriority);
        Assert.Equal("Category 'Security' is always treated as Critical", result.Triage.PriorityReason);
        Assert.Equal(4, result.Triage.SlaWindowHours);
        Assert.Equal("Critical base window 4h", result.Triage.SlaReason);
        Assert.Equal("Fewest open tickets among Security specialists", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WithNobodyEligible_SavesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext { Category = SeedDesk.Security, Desk = [SeedDesk.Marcus] };

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Equal("No available agent specialised in Security", result.Triage!.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext { Customer = null };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext { Category = null };

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
