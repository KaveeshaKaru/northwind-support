using FluentValidation;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

public sealed class SlaSummaryQueryValidator : AbstractValidator<SlaSummaryQuery>
{
    public SlaSummaryQueryValidator()
    {
        RuleFor(x => x.From).NotNull().WithMessage("Give the start of the period.");

        RuleFor(x => x.To)
            .NotNull()
            .WithMessage("Give the end of the period.")
            .GreaterThan(x => x.From)
            .When(x => x.From is not null)
            .WithMessage("The end of the period must be after its start.");

        RuleFor(x => x.GroupBy)
            .NotNull()
            .IsInEnum()
            .WithMessage("Group by 'agent' or 'priority'.");
    }
}
