using Microsoft.AspNetCore.Identity;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2 (HMAC-SHA512, 100,000
/// iterations) with a random 128-bit salt, stored in a versioned format so the algorithm can be
/// strengthened later without breaking existing hashes.
/// </summary>
public sealed class AgentPasswordHasher : Application.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<Agent> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(user: null!, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(user: null!, hash, password) is not PasswordVerificationResult.Failed;
}
