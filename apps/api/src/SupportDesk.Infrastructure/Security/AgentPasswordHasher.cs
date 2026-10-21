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

    /// <summary>
    /// Checked when there is no real hash (unknown email, no password set), so that answer takes
    /// as long as a wrong password does. Its password is random and thrown away.
    /// </summary>
    private readonly string _decoyHash;

    public AgentPasswordHasher() => _decoyHash = Hash(Guid.NewGuid().ToString());

    public string Hash(string password) => _hasher.HashPassword(user: null!, password);

    public bool Verify(string? hash, string password)
    {
        var matches = _hasher.VerifyHashedPassword(user: null!, hash ?? _decoyHash, password)
            is not PasswordVerificationResult.Failed;

        return hash is not null && matches;
    }
}
