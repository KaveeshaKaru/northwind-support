using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class AgentPasswordHasherTests
{
    private const string Password = "LocalDev-Only-Password1!";
    private readonly AgentPasswordHasher _hasher = new();

    [Fact]
    public void Hash_NeverContainsThePassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.DoesNotContain(Password, hash);
        Assert.NotEqual(Password, hash);
    }

    [Fact]
    public void Hash_IsSalted_SoTheSamePasswordHashesDifferentlyEachTime() =>
        Assert.NotEqual(_hasher.Hash(Password), _hasher.Hash(Password));

    [Fact]
    public void Verify_TheRightPassword_Succeeds() =>
        Assert.True(_hasher.Verify(_hasher.Hash(Password), Password));

    [Theory]
    [InlineData("localdev-only-password1!")]
    [InlineData("LocalDev-Only-Password1")]
    [InlineData("")]
    public void Verify_AnyOtherPassword_Fails(string attempt) =>
        Assert.False(_hasher.Verify(_hasher.Hash(Password), attempt));
}
