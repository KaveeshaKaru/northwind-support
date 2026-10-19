using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

/// <summary>
/// A real, empty database for query tests: SQLite in memory, created from the EF model and
/// thrown away when the test ends. Rows are added through the aggregates' own behaviour.
/// </summary>
/// <remarks>
/// SQLite is close to SQL Server but not identical - notably, its string matching is
/// case-sensitive where SQL Server's default collation is not.
/// </remarks>
internal sealed class SqliteSupportDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteSupportDatabase()
    {
        // The in-memory database lives as long as this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new SupportDbContext(options);
        Db.Database.EnsureCreated();
    }

    public SupportDbContext Db { get; }

    public Customer AddCustomer(string name, CustomerTier tier = CustomerTier.Standard) =>
        Save(new Customer(name, $"{Guid.NewGuid():N}@example.com", null, tier, FixedClock.DefaultNow));

    public Category AddCategory(string name, bool requiresSpecialist = false, bool forcesCriticalPriority = false) =>
        Save(new Category(name, requiresSpecialist, forcesCriticalPriority));

    public Agent AddAgent(string fullName, int maxOpenTickets = 10) =>
        Save(new Agent(fullName, $"{Guid.NewGuid():N}@example.com", maxOpenTickets, FixedClock.DefaultNow));

    public Ticket AddTicket(
        string reference,
        Customer customer,
        Category category,
        string title = "Something is not working",
        TicketPriority priority = TicketPriority.Medium,
        TicketStatus status = TicketStatus.New,
        Agent? agent = null)
    {
        var ticket = Ticket.Raise(
            reference, title, "A description long enough to be realistic.",
            customer.Id, category.Id, priority, FixedClock.DefaultNow);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, FixedClock.DefaultNow);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, FixedClock.DefaultNow);
        }

        return Save(ticket);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private T Save<T>(T entity)
        where T : class
    {
        Db.Add(entity);
        Db.SaveChanges();

        // Queries under test must read from the database, not from what this context remembers.
        Db.ChangeTracker.Clear();

        return entity;
    }
}
