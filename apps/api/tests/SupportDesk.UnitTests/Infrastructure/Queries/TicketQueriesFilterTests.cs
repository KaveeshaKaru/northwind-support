using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list's filters, run against a real database so that what is tested is the SQL
/// EF Core generates, not an in-memory imitation of it.
/// </summary>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly SqliteSupportDatabase _database = new();
    private readonly TicketQueries _queries;

    private readonly Customer _contoso;
    private readonly Customer _fabrikam;
    private readonly Category _billing;
    private readonly Category _technical;
    private readonly Agent _alex;
    private readonly Agent _sara;

    public TicketQueriesFilterTests()
    {
        _queries = new TicketQueries(_database.Db, new FixedClock());

        _contoso = _database.AddCustomer("Contoso Ltd", CustomerTier.Premium);
        _fabrikam = _database.AddCustomer("Fabrikam Inc");
        _billing = _database.AddCategory("Billing");
        _technical = _database.AddCategory("Technical");
        _alex = _database.AddAgent("Alex Turner");
        _sara = _database.AddAgent("Sara Lindqvist");
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Priority_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, priority: TicketPriority.High);
        _database.AddTicket("TCK-0002", _fabrikam, _technical, priority: TicketPriority.High);
        _database.AddTicket("TCK-0003", _contoso, _technical, priority: TicketPriority.Low);
        _database.AddTicket("TCK-0004", _contoso, _billing, priority: TicketPriority.Critical);

        var result = await Search(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(["TCK-0001", "TCK-0002"], References(result));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Status_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, status: TicketStatus.InProgress);
        _database.AddTicket("TCK-0002", _contoso, _billing, status: TicketStatus.Open);

        var result = await Search(new TicketQuery { Status = TicketStatus.InProgress });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Category_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _contoso, _technical);

        var result = await Search(new TicketQuery { CategoryId = _technical.Id });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task Customer_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _fabrikam, _billing);

        var result = await Search(new TicketQuery { CustomerId = _fabrikam.Id });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task AssignedAgent_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, agent: _alex);
        _database.AddTicket("TCK-0002", _contoso, _billing, agent: _sara);
        _database.AddTicket("TCK-0003", _contoso, _billing);

        var result = await Search(new TicketQuery { AssignedAgentId = _sara.Id });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task UnassignedOnly_ReturnsOnlyTicketsWithoutAnAgent()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, agent: _alex);
        _database.AddTicket("TCK-0002", _contoso, _billing);

        var result = await Search(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task UnassignedOnly_WithAnAgent_ReturnsNothing()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, agent: _alex);
        _database.AddTicket("TCK-0002", _contoso, _billing);

        var result = await Search(new TicketQuery { UnassignedOnly = true, AssignedAgentId = _alex.Id });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesCustomerName()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, title: "Invoice is wrong");
        _database.AddTicket("TCK-0002", _fabrikam, _billing, title: "Invoice is late");

        var result = await Search(new TicketQuery { Search = "Contoso" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Search_MatchesReference()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _contoso, _billing);

        var result = await Search(new TicketQuery { Search = "TCK-0002" });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task Search_MatchesPartOfTheTitle()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, title: "Duplicate charge on invoice");
        _database.AddTicket("TCK-0002", _contoso, _billing, title: "Cannot log in");

        var result = await Search(new TicketQuery { Search = "charge" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Search_IsTrimmed()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _fabrikam, _billing);

        var result = await Search(new TicketQuery { Search = "  Contoso  " });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Search_OfOnlyWhitespace_IsIgnored()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _fabrikam, _billing);

        var result = await Search(new TicketQuery { Search = "   " });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Filters_CombineWithAnd()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing, priority: TicketPriority.High, status: TicketStatus.Open);
        _database.AddTicket("TCK-0002", _contoso, _billing, priority: TicketPriority.High, status: TicketStatus.Resolved);
        _database.AddTicket("TCK-0003", _contoso, _billing, priority: TicketPriority.Low, status: TicketStatus.Open);
        _database.AddTicket("TCK-0004", _fabrikam, _billing, priority: TicketPriority.High, status: TicketStatus.Open);

        var result = await Search(new TicketQuery
        {
            CustomerId = _contoso.Id,
            Priority = TicketPriority.High,
            Status = TicketStatus.Open
        });

        Assert.Equal(["TCK-0001"], References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Count_IsForTheFilteredSet_NotThePage()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _contoso, _billing);
        _database.AddTicket("TCK-0003", _contoso, _billing);
        _database.AddTicket("TCK-0004", _fabrikam, _billing);

        var result = await Search(new TicketQuery { CustomerId = _contoso.Id, PageSize = 1 });

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task PageBeyondTheEnd_IsEmpty_WithTheFilteredCount()
    {
        _database.AddTicket("TCK-0001", _contoso, _billing);
        _database.AddTicket("TCK-0002", _contoso, _billing);
        _database.AddTicket("TCK-0003", _fabrikam, _billing);

        var result = await Search(new TicketQuery { CustomerId = _contoso.Id, Page = 5 });

        Assert.Empty(result.Items);
        Assert.Equal(2, result.TotalCount);
    }

    private Task<PagedResult<TicketListItemDto>> Search(TicketQuery query) =>
        _queries.GetPagedAsync(query, CancellationToken.None);

    /// <summary>The references returned, sorted so tests compare sets rather than sort order.</summary>
    private static string[] References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(i => i.Reference).Order().ToArray();
}
