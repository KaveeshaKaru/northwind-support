using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates;

public class AgentTests
{
    private static Agent NewAgent() => new("Alex Turner", "alex@example.com", 10, FixedClock.DefaultNow);

    [Fact]
    public void ANewAgent_HasNoPassword() => Assert.Null(NewAgent().PasswordHash);

    [Fact]
    public void SetPasswordHash_StoresTheHash()
    {
        var agent = NewAgent();

        agent.SetPasswordHash("AQAAAAIAAYagAAAAE...");

        Assert.Equal("AQAAAAIAAYagAAAAE...", agent.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_RejectsABlankHash(string hash) =>
        Assert.Throws<ArgumentException>(() => NewAgent().SetPasswordHash(hash));
}
