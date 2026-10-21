using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

/// <summary>AU-4: one generic failure for every way a sign-in can be wrong.</summary>
public class LoginCommandHandlerTests
{
    private const string Email = "sara.lindqvist@northwind-support.example";
    private const string Password = "LocalDev-Only-Password1!";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly CountingHasher _hasher = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<Agent>()))
            .Returns(new IssuedToken("signed.jwt.token", FixedClock.DefaultNow.AddMinutes(60)));

        _handler = new LoginCommandHandler(_agents.Object, _hasher, _tokens.Object);
    }

    private Agent ExistingAgent(bool isActive = true, bool hasPassword = true)
    {
        var agent = new Agent("Sara Lindqvist", Email, 6, FixedClock.DefaultNow);

        if (hasPassword)
        {
            agent.SetPasswordHash(_hasher.Hash(Password));
        }

        if (!isActive)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        return agent;
    }

    [Fact]
    public async Task TheRightPassword_ReturnsATokenItsExpiryAndWhoSignedIn()
    {
        ExistingAgent();

        var result = await _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.Equal("signed.jwt.token", result.Token);
        Assert.Equal(FixedClock.DefaultNow.AddMinutes(60), result.ExpiresAtUtc);
        Assert.Equal("Sara Lindqvist", result.Agent.FullName);
        Assert.Equal(Email, result.Agent.Email);
    }

    [Fact]
    public async Task TheEmail_IsTrimmedBeforeLookingTheAgentUp()
    {
        ExistingAgent();

        var result = await _handler.HandleAsync(new LoginRequest($"  {Email} ", Password), CancellationToken.None);

        Assert.Equal(Email, result.Agent.Email);
    }

    [Fact]
    public async Task AWrongPassword_IsUnauthorized_AndIssuesNoToken()
    {
        ExistingAgent();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "wrong-password"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task AnUnknownEmail_FailsExactlyLikeAWrongPassword()
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task AnUnknownEmail_StillPaysForAPasswordCheck_SoTimingDoesNotGiveItAway()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(1, _hasher.Verifications);
    }

    [Fact]
    public async Task AnInactiveAgent_CannotSignIn_EvenWithTheRightPassword()
    {
        ExistingAgent(isActive: false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
    }

    [Fact]
    public async Task AnAgentWithoutAPassword_CannotSignIn()
    {
        ExistingAgent(hasPassword: false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));
    }

    /// <summary>The real hasher, counting how often a password is checked.</summary>
    private sealed class CountingHasher : IPasswordHasher
    {
        private readonly AgentPasswordHasher _inner = new();

        public int Verifications { get; private set; }

        public string Hash(string password) => _inner.Hash(password);

        public bool Verify(string? hash, string password)
        {
            Verifications++;
            return _inner.Verify(hash, password);
        }
    }
}
