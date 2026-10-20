using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Presentation.Extensions;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Signs an agent in. 401 with the same message whether the email or the password was wrong.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The agent the bearer token belongs to.</summary>
    [Authorize]
    [HttpGet("me")]
    public Task<CurrentAgentDto> Me([FromServices] GetCurrentAgentQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(User.AgentId(), ct);
}
