using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

/// <summary>AU-3: development credentials are seeded hashed, on a new database and an existing one.</summary>
public sealed class SupportDbSeederCredentialsTests : IDisposable
{
    private readonly SqliteSupportDatabase _database = new();
    private readonly AgentPasswordHasher _hasher = new();

    public void Dispose() => _database.Dispose();

    private SupportDbSeeder Seeder() =>
        new(_database.Db, new FixedClock(), TestSla.Settings(), _hasher, NullLogger<SupportDbSeeder>.Instance);

    [Fact]
    public async Task ANewDatabase_GetsEveryAgentWithTheHashedDevelopmentPassword()
    {
        await Seeder().SeedAsync();

        var agents = await _database.Db.Set<Agent>().AsNoTracking().ToListAsync();

        Assert.Equal(6, agents.Count);
        Assert.All(agents, agent =>
        {
            Assert.NotNull(agent.PasswordHash);
            Assert.NotEqual(SupportDbSeeder.DevelopmentPassword, agent.PasswordHash);
            Assert.True(_hasher.Verify(agent.PasswordHash, SupportDbSeeder.DevelopmentPassword));
        });
    }

    [Fact]
    public async Task AnAlreadySeededDatabase_HasMissingPasswordsFilledIn()
    {
        var customer = _database.AddCustomer("Contoso Ltd");
        var category = _database.AddCategory("General");
        var agent = _database.AddAgent("Alex Turner");
        _database.AddTicket("TCK-0001", customer, category, agent: agent);

        await Seeder().SeedAsync();

        var stored = await _database.Db.Set<Agent>().AsNoTracking().SingleAsync();
        Assert.True(_hasher.Verify(stored.PasswordHash!, SupportDbSeeder.DevelopmentPassword));
    }
}
