using FluentValidation.TestHelper;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.UnitTests.Application.Features.Reports;

public class SlaSummaryQueryValidatorTests
{
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SlaSummaryQueryValidator _validator = new();

    private static SlaSummaryQuery Query(
        DateTime? from = null, DateTime? to = null, SlaSummaryGroupBy? groupBy = SlaSummaryGroupBy.Agent) =>
        new() { From = from ?? From, To = to ?? From.AddDays(30), GroupBy = groupBy };

    [Fact]
    public void AValidQuery_Passes() =>
        _validator.TestValidate(Query()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void From_Missing_Fails() =>
        _validator.TestValidate(Query() with { From = null }).ShouldHaveValidationErrorFor(q => q.From);

    [Fact]
    public void To_Missing_Fails() =>
        _validator.TestValidate(Query() with { To = null }).ShouldHaveValidationErrorFor(q => q.To);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void To_NotAfterFrom_Fails(int days) =>
        _validator.TestValidate(Query(to: From.AddDays(days))).ShouldHaveValidationErrorFor(q => q.To);

    [Fact]
    public void GroupBy_Missing_Fails() =>
        _validator.TestValidate(Query(groupBy: null)).ShouldHaveValidationErrorFor(q => q.GroupBy);

    [Fact]
    public void GroupBy_NotAgentOrPriority_Fails() =>
        _validator.TestValidate(Query(groupBy: (SlaSummaryGroupBy)99)).ShouldHaveValidationErrorFor(q => q.GroupBy);
}
