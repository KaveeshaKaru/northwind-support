using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class AgentWorkloadsTests : IDisposable
{
    private readonly SqliteSupportDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task OpenTickets_CountsOnlyTicketsThatAreNotResolvedOrClosed()
    {
        var customer = _database.AddCustomer("Contoso Ltd");
        var category = _database.AddCategory("General");
        var alex = _database.AddAgent("Alex Turner", maxOpenTickets: 10);

        _database.AddTicket("TCK-0001", customer, category, agent: alex, status: TicketStatus.New);
        _database.AddTicket("TCK-0002", customer, category, agent: alex, status: TicketStatus.InProgress);
        _database.AddTicket("TCK-0003", customer, category, agent: alex, status: TicketStatus.Resolved);
        _database.AddTicket("TCK-0004", customer, category, agent: alex, status: TicketStatus.Closed);
        _database.AddTicket("TCK-0005", customer, category);

        var workload = Assert.Single(await new AgentWorkloads(_database.Db).GetAllAsync(CancellationToken.None));

        Assert.Equal(alex.Id, workload.AgentId);
        Assert.Equal(2, workload.OpenTickets);
        Assert.Equal(10, workload.MaxOpenTickets);
        Assert.True(workload.IsActive);
    }

    [Fact]
    public async Task Specializations_AreTheCategoryIdsTheAgentHandles()
    {
        var billing = _database.AddCategory("Billing", requiresSpecialist: true);
        var security = _database.AddCategory("Security", requiresSpecialist: true);
        _database.AddCategory("General");
        var marcus = _database.AddAgent("Marcus Doyle", specializations: [billing, security]);
        _database.AddAgent("Alex Turner");

        var workloads = await new AgentWorkloads(_database.Db).GetAllAsync(CancellationToken.None);

        Assert.Equal([billing.Id, security.Id], workloads.Single(w => w.AgentId == marcus.Id).SpecializationCategoryIds.Order());
        Assert.Empty(workloads.Single(w => w.AgentId != marcus.Id).SpecializationCategoryIds);
    }
}
