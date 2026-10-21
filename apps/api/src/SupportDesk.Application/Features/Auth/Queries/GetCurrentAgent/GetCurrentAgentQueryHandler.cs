using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Returns the agent a valid token belongs to (AU-5). An agent deactivated since signing in is
/// no longer accepted.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">The agent no longer exists or is inactive.</exception>
    public async Task<CurrentAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("Your session is no longer valid. Please sign in again.");
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
