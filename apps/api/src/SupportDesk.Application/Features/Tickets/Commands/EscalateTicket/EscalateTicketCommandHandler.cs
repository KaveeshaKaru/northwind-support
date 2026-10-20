using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket (BR-7) through the same triage rules as a new ticket, and records who did
/// it and why. Who did it is the signed-in agent, never a name taken from the request.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentWorkloads agentWorkloads,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ITicketQueries ticketQueries,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is already Critical, or Resolved or Closed.
    /// </exception>
    public async Task<TicketDetailDto> HandleAsync(
        int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var decision = triage.ForEscalation(ticket, category, customer.Tier, await agentWorkloads.GetAllAsync(ct), now);
        var escalation = ticket.Escalate(decision, request.Reason, escalatedBy, now);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated by {EscalatedBy} from {From} to {To}.",
            ticket.Reference, escalation.EscalatedBy, escalation.FromPriority, escalation.ToPriority);

        var updated = await getTicket.HandleAsync(id, ct);
        updated.Triage = TriageDto.From(decision);
        updated.Escalation = (await ticketQueries.GetEscalationsAsync(id, ct))?.First(e => e.Id == escalation.Id);

        return updated;
    }
}
