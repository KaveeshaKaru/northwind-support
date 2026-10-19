using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// <c>?slaStatus=</c> runs the same rule as the status shown on each ticket, in the database.
/// One ticket in each state, "now" fixed at <see cref="FixedClock.DefaultNow"/>.
/// </summary>
public sealed class TicketQueriesSlaFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteSupportDatabase _database = new();
    private readonly TicketQueries _queries;

    public TicketQueriesSlaFilterTests()
    {
        _queries = new TicketQueries(_database.Db, new FixedClock());

        Customer customer = _database.AddCustomer("Contoso Ltd");
        Category category = _database.AddCategory("General");

        _database.AddTicket("TCK-NA", customer, category);
        _database.AddTicketWithSla("TCK-WITHIN", customer, category, Now.AddHours(8), Now.AddHours(6), priority: TicketPriority.High);
        _database.AddTicketWithSla("TCK-RISK", customer, category, Now.AddHours(1), Now.AddHours(-1), priority: TicketPriority.High);
        _database.AddTicketWithSla("TCK-LATE", customer, category, Now.AddHours(-1), Now.AddHours(-3));
        _database.AddTicketWithSla("TCK-MET", customer, category, Now.AddHours(2), Now.AddHours(1), resolvedAtUtc: Now.AddHours(1));
        _database.AddTicketWithSla("TCK-MISSED", customer, category, Now.AddHours(-2), Now.AddHours(-3), resolvedAtUtc: Now.AddHours(-1));
    }

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData(SlaStatus.NotApplicable, new[] { "TCK-NA" })]
    [InlineData(SlaStatus.WithinSla, new[] { "TCK-WITHIN" })]
    [InlineData(SlaStatus.AtRisk, new[] { "TCK-RISK" })]
    [InlineData(SlaStatus.Breached, new[] { "TCK-LATE", "TCK-MISSED" })]
    [InlineData(SlaStatus.Met, new[] { "TCK-MET" })]
    public async Task SlaStatus_ReturnsOnlyTicketsInThatState(SlaStatus status, string[] expected)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = status }, CancellationToken.None);

        Assert.Equal(expected.Order(), result.Items.Select(i => i.Reference).Order());
        Assert.All(result.Items, item => Assert.Equal(status, item.SlaStatus));
        Assert.Equal(expected.Length, result.TotalCount);
    }

    [Fact]
    public async Task SlaStatus_CombinesWithOtherFilters()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.AtRisk, Priority = TicketPriority.Medium },
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SlaStatus_CountIsForTheFilteredSet_NotThePage()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 1 },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }
}
