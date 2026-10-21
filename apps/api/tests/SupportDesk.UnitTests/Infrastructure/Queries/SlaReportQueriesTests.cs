using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA summary (2.5): one aggregation in the database, using the same SLA rule as the list.
/// Every ticket is raised at <see cref="Raised"/>; the report runs ten hours later.
/// </summary>
public sealed class SlaReportQueriesTests : IDisposable
{
    private static readonly DateTime Raised = FixedClock.DefaultNow;
    private static readonly DateTime Now = Raised.AddHours(10);
    private static readonly DateTime From = Raised.AddDays(-1);
    private static readonly DateTime To = Raised.AddDays(1);

    private readonly SqlCommandRecorder _sql = new();
    private readonly SqliteSupportDatabase _database;
    private readonly SlaReportQueries _reports;

    public SlaReportQueriesTests()
    {
        _database = new SqliteSupportDatabase(_sql);
        _reports = new SlaReportQueries(_database.Db, new FixedClock(Now));

        var customer = _database.AddCustomer("Contoso Ltd");
        var category = _database.AddCategory("General");
        Agent alex = _database.AddAgent("Alex Turner");
        Agent sara = _database.AddAgent("Sara Lindqvist");

        // Open and past due at "now" -> Breached.
        _database.AddTicketWithSla("TCK-LATE", customer, category, Raised.AddHours(8), Raised.AddHours(6), priority: TicketPriority.High, agent: alex);
        // Open, inside the last part of the window -> AtRisk.
        _database.AddTicketWithSla("TCK-RISK", customer, category, Raised.AddHours(12), Raised.AddHours(9), priority: TicketPriority.High, agent: sara);
        // Open with plenty of time -> WithinSla.
        _database.AddTicketWithSla("TCK-OK", customer, category, Raised.AddHours(24), Raised.AddHours(18));
        // Resolved after 4 hours, on time -> Met.
        _database.AddTicketWithSla("TCK-MET", customer, category, Raised.AddHours(24), Raised.AddHours(18), resolvedAtUtc: Raised.AddHours(4), agent: alex);
        // Resolved after 6 hours, too late -> Breached.
        _database.AddTicketWithSla("TCK-MISSED", customer, category, Raised.AddHours(2), Raised.AddHours(1), resolvedAtUtc: Raised.AddHours(6));
        // No SLA at all.
        _database.AddTicket("TCK-NOSLA", customer, category, priority: TicketPriority.Low);

        _sql.Commands.Clear();
    }

    public void Dispose() => _database.Dispose();

    private Task<IReadOnlyList<SlaSummaryRowDto>> Summary(SlaSummaryGroupBy groupBy, DateTime? from = null, DateTime? to = null) =>
        _reports.GetSlaSummaryAsync(from ?? From, to ?? To, groupBy, CancellationToken.None);

    [Fact]
    public async Task ByPriority_CountsEachSlaState_HighestPriorityFirst()
    {
        var rows = await Summary(SlaSummaryGroupBy.Priority);

        Assert.Equal(
            [
                new SlaSummaryRowDto("High", Total: 2, Breached: 1, AtRisk: 1, WithinSla: 0, Met: 0, BreachRate: 0.5, AverageResolutionHours: null),
                new SlaSummaryRowDto("Medium", Total: 3, Breached: 1, AtRisk: 0, WithinSla: 1, Met: 1, BreachRate: 0.333, AverageResolutionHours: 5.0),
                new SlaSummaryRowDto("Low", Total: 1, Breached: 0, AtRisk: 0, WithinSla: 0, Met: 0, BreachRate: null, AverageResolutionHours: null),
            ],
            rows);
    }

    [Fact]
    public async Task ByAgent_NamesEachAgent_AndGroupsUnassignedTicketsLast()
    {
        var rows = await Summary(SlaSummaryGroupBy.Agent);

        Assert.Equal(
            [
                new SlaSummaryRowDto("Alex Turner", Total: 2, Breached: 1, AtRisk: 0, WithinSla: 0, Met: 1, BreachRate: 0.5, AverageResolutionHours: 4.0),
                new SlaSummaryRowDto("Sara Lindqvist", Total: 1, Breached: 0, AtRisk: 1, WithinSla: 0, Met: 0, BreachRate: 0.0, AverageResolutionHours: null),
                new SlaSummaryRowDto("Unassigned", Total: 3, Breached: 1, AtRisk: 0, WithinSla: 1, Met: 0, BreachRate: 0.5, AverageResolutionHours: 6.0),
            ],
            rows);
    }

    [Fact]
    public async Task OnlyTicketsRaisedInThePeriod_AreCounted_FromInclusiveToExclusive()
    {
        Assert.Equal(6, (await Summary(SlaSummaryGroupBy.Priority, from: Raised, to: Raised.AddTicks(1))).Sum(r => r.Total));
        Assert.Empty(await Summary(SlaSummaryGroupBy.Priority, from: Raised.AddTicks(1), to: To));
        Assert.Empty(await Summary(SlaSummaryGroupBy.Priority, from: From, to: Raised));
    }

    [Theory]
    [InlineData(SlaSummaryGroupBy.Agent)]
    [InlineData(SlaSummaryGroupBy.Priority)]
    public async Task IsOneGroupedQuery_NotTicketsPulledIntoMemory(SlaSummaryGroupBy groupBy)
    {
        await Summary(groupBy);

        var query = Assert.Single(_sql.Commands);
        Assert.Contains("GROUP BY", query);
        Assert.Contains("COUNT(", query);
        Assert.Contains("AVG(", query);
    }
}
