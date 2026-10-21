using FluentValidation.TestHelper;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private static EscalateTicketRequest Request(string reason = "Customer's site is down.") => new(reason);

    [Fact]
    public void AValidRequest_Passes() =>
        _validator.TestValidate(Request()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void Reason_AtTheLimits_Passes(int length) =>
        _validator.TestValidate(Request(reason: new string('x', length))).ShouldNotHaveValidationErrorFor(r => r.Reason);

    [Theory]
    [InlineData(4)]
    [InlineData(501)]
    public void Reason_OutsideTheLimits_Fails(int length) =>
        _validator.TestValidate(Request(reason: new string('x', length))).ShouldHaveValidationErrorFor(r => r.Reason);

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("  abc  ")]
    public void Reason_ThatIsBlankOrTooShortOnceTrimmed_Fails(string reason) =>
        _validator.TestValidate(Request(reason: reason)).ShouldHaveValidationErrorFor(r => r.Reason);

    [Fact]
    public void Reason_Missing_Fails() =>
        _validator.TestValidate(Request(reason: null!)).ShouldHaveValidationErrorFor(r => r.Reason);
}
