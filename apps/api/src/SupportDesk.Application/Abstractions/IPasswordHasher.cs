namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into something safe to store, and checks a password against it. The
/// mechanism (salted, slow, versioned) is an infrastructure concern.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
