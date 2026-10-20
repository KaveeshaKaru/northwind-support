using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SupportDesk.Presentation.Extensions;

/// <summary>Who the caller is, read from their validated access token.</summary>
public static class ClaimsPrincipalExtensions
{
    public static int AgentId(this ClaimsPrincipal user) =>
        int.Parse(Required(user, JwtRegisteredClaimNames.Sub), CultureInfo.InvariantCulture);

    public static string AgentEmail(this ClaimsPrincipal user) => Required(user, JwtRegisteredClaimNames.Email);

    private static string Required(ClaimsPrincipal user, string claimType) =>
        user.FindFirstValue(claimType)
        ?? throw new InvalidOperationException($"The access token has no '{claimType}' claim.");
}
