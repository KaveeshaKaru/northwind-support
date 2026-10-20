using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// How access tokens are signed and checked. Bound from the <c>Jwt</c> configuration section;
/// the signing key comes from configuration or the <c>Jwt__SigningKey</c> environment variable,
/// never from code.
/// </summary>
public sealed record JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    /// <summary>How long a token works after sign-in. There are no refresh tokens.</summary>
    public int LifetimeMinutes { get; init; }

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>What a presented token must satisfy: our issuer, our audience, our key, not expired.</summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = Key(),
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    /// <summary>What is wrong with these settings; empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumKeyBytes)
        {
            errors.Add($"Jwt:SigningKey must be at least {MinimumKeyBytes} bytes. Set it with the Jwt__SigningKey environment variable.");
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            errors.Add("Jwt:Issuer and Jwt:Audience are required.");
        }

        if (LifetimeMinutes <= 0)
        {
            errors.Add("Jwt:LifetimeMinutes must be positive.");
        }

        return errors;
    }
}
