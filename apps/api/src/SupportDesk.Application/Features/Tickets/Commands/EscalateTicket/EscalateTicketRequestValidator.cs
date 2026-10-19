using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    private const int MinLength = TicketEscalation.ReasonMinLength;
    private const int MaxLength = TicketEscalation.ReasonMaxLength;

    public EscalateTicketRequestValidator()
    {
        // Counted after trimming, because that is what gets stored.
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Give a reason for the escalation.")
            .Must(reason => reason.Trim().Length is >= MinLength and <= MaxLength)
            .WithMessage($"The reason must be between {MinLength} and {MaxLength} characters.");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty()
            .WithMessage("Say who is escalating the ticket.")
            .MaximumLength(TicketEscalation.EscalatedByMaxLength);
    }
}
