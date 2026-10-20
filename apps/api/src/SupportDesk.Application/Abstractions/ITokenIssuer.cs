using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>Issues the access token an agent presents on every call after signing in.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Agent agent);
}

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);
