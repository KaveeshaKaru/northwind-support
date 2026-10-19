using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Tickets.Triage;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketEscalationQueriesTests : IDisposable
{
    private readonly SqliteSupportDatabase _database = new();
    private readonly TicketQueries _queries;

    public TicketEscalationQueriesTests() => _queries = new TicketQueries(_database.Db, new FixedClock());

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task History_IsNewestFirst_WithBeforeAndAfterAgents()
    {
        var customer = _database.AddCustomer("Contoso Ltd");
        var category = _database.AddCategory("General");
        var alex = _database.AddAgent("Alex Turner");
        var sara = _database.AddAgent("Sara Lindqvist");
        var ticket = _database.AddTicket("TCK-0001", customer, category, priority: TicketPriority.Low, agent: alex);

        Escalate(ticket.Id, toAgent: sara, reason: "First escalation", at: FixedClock.DefaultNow.AddHours(1));
        Escalate(ticket.Id, toAgent: null, reason: "Second escalation", at: FixedClock.DefaultNow.AddHours(2));

        var history = await _queries.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal(["Second escalation", "First escalation"], history.Select(e => e.Reason));

        var first = history[1];
        Assert.Equal(TicketPriority.Low, first.FromPriority);
        Assert.Equal(TicketPriority.Medium, first.ToPriority);
        Assert.Equal("Alex Turner", first.FromAgent?.FullName);
        Assert.Equal("Sara Lindqvist", first.ToAgent?.FullName);
        Assert.Equal("alex.lead", first.EscalatedBy);
        Assert.Null(history[0].ToAgent);
    }

    [Fact]
    public async Task History_OfATicketNeverEscalated_IsEmpty()
    {
        var ticket = _database.AddTicket("TCK-0001", _database.AddCustomer("Contoso Ltd"), _database.AddCategory("General"));

        var history = await _queries.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task History_OfAnUnknownTicket_IsNull() =>
        Assert.Null(await _queries.GetEscalationsAsync(999, CancellationToken.None));

    /// <summary>Escalates through the aggregate, as the application does, and saves it.</summary>
    private void Escalate(int ticketId, Agent? toAgent, string reason, DateTime at)
    {
        var ticket = _database.Db.Set<Ticket>().Single(t => t.Id == ticketId);
        var priority = ticket.EscalatedPriority();
        var decision = new TriageDecision(
            priority,
            "Escalated",
            new SlaTerms(TimeSpan.FromHours(8), at.AddHours(8), at.AddHours(6), "High base window 8h"),
            new AssignmentDecision(toAgent?.Id, "Test"));

        ticket.Escalate(decision, reason, "alex.lead", at);
        _database.Db.SaveChanges();
        _database.Db.ChangeTracker.Clear();
    }
}
