namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into something safe to store, and checks a password against it. The
/// mechanism (salted, slow, versioned) is an infrastructure concern.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="hash"/>. With no hash it
    /// still does the same work before saying no, so callers cannot be timed.
    /// </summary>
    bool Verify(string? hash, string password);
}
