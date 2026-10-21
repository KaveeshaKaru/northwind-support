using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with email and password (AU-4). Every way of getting it wrong - unknown
/// email, wrong password, inactive agent, no password set - gives the same answer, and the
/// password check runs either way so the response time does not tell them apart.
/// </summary>
public sealed class LoginCommandHandler(IAgentRepository agents, IPasswordHasher passwordHasher, ITokenIssuer tokens)
{
    public const string FailureMessage = "Invalid email or password.";

    /// <exception cref="UnauthorizedException">The credentials were not accepted.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        var passwordMatches = passwordHasher.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !agent.IsActive || !passwordMatches)
        {
            throw new UnauthorizedException(FailureMessage);
        }

        var token = tokens.Issue(agent);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, new CurrentAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
