using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// <see cref="SqlDate.HoursBetween"/> is translated differently per database. The SQLite version
/// runs in the query tests; SQL Server's is checked here from the SQL it generates, with no
/// server needed.
/// </summary>
public sealed class SqlDateTests
{
    [Fact]
    public void OnSqlServer_DividesAFloat_SoTheResultReadsBackAsADouble()
    {
        // A decimal literal (3600.0) on its own makes SQL Server return decimal, which EF cannot
        // read into a double - the query failed at run time until the cast was added.
        using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=not-used-only-for-generating-sql")
            .Options);

        var sql = db.Set<Ticket>().Select(t => SqlDate.HoursBetween(t.CreatedAtUtc, t.ResolvedAtUtc)).ToQueryString();

        Assert.Contains("CAST(DATEDIFF_BIG(second, [t].[CreatedAtUtc], [t].[ResolvedAtUtc]) AS float)", sql);
    }

    [Fact]
    public async Task OnSqlite_GivesTheHoursBetweenTheDates()
    {
        using var database = new SqliteSupportDatabase();
        var customer = database.AddCustomer("Contoso Ltd");
        var category = database.AddCategory("General");
        var raised = FixedClock.DefaultNow;
        database.AddTicketWithSla("TCK-1", customer, category, raised.AddHours(8), raised.AddHours(6), resolvedAtUtc: raised.AddMinutes(90));
        database.AddTicket("TCK-2", customer, category);

        var hours = await database.Db.Set<Ticket>()
            .OrderBy(t => t.Reference)
            .Select(t => SqlDate.HoursBetween(t.CreatedAtUtc, t.ResolvedAtUtc))
            .ToListAsync();

        Assert.Equal(1.5, hours[0]!.Value, precision: 6);
        Assert.Null(hours[1]);
    }
}
