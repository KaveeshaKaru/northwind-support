using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class JwtTokenIssuerTests
{
    private static readonly JwtSettings Settings = new()
    {
        Issuer = "SupportDesk",
        Audience = "SupportDesk",
        SigningKey = "test-only-signing-key-0123456789abcdef",
        LifetimeMinutes = 60
    };

    private static Agent Sara()
    {
        var agent = new Agent("Sara Lindqvist", "sara@example.com", 6, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, 4);
        return agent;
    }

    [Fact]
    public async Task TheToken_IsSigned_AndCarriesWhoTheAgentIs()
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var issued = new JwtTokenIssuer(Settings, clock).Issue(Sara());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Token, Settings.ValidationParameters());

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("4", result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("sara@example.com", result.Claims[JwtRegisteredClaimNames.Email]);
        Assert.Equal("Sara Lindqvist", result.Claims[JwtRegisteredClaimNames.Name]);
    }

    [Fact]
    public void TheToken_ExpiresAfterTheConfiguredLifetime()
    {
        var issued = new JwtTokenIssuer(Settings, new FixedClock()).Issue(Sara());

        Assert.Equal(FixedClock.DefaultNow.AddMinutes(60), issued.ExpiresAtUtc);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRejected()
    {
        var forger = new JwtTokenIssuer(Settings with { SigningKey = "someone-elses-key-0123456789abcdef!!" }, new FixedClock(DateTime.UtcNow));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(forger.Issue(Sara()).Token, Settings.ValidationParameters());

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("")]
    public void ASigningKeyShorterThan32Bytes_IsReported(string key) =>
        Assert.NotEmpty((Settings with { SigningKey = key }).Validate());
}
