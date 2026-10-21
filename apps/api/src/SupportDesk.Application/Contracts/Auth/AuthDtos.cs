namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent: enough for the app to greet them.</summary>
public sealed record CurrentAgentDto(int Id, string FullName, string Email);

/// <summary>A successful sign-in: the bearer token, when it stops working, and who it is for.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentAgentDto Agent);
