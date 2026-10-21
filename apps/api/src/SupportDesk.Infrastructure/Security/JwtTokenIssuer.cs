using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs naming the agent: their id (<c>sub</c>), email and name.
/// Nothing else - no roles, no password material.
/// </summary>
public sealed class JwtTokenIssuer(JwtSettings settings, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(settings.LifetimeMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Email, agent.Email),
                new Claim(JwtRegisteredClaimNames.Name, agent.FullName)
            ]),
            SigningCredentials = new SigningCredentials(settings.Key(), SecurityAlgorithms.HmacSha256)
        });

        return new IssuedToken(token, expiresAtUtc);
    }
}
