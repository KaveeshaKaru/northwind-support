using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(GetSlaSummaryQueryHandler getSlaSummary) : ControllerBase
{
    /// <summary>
    /// SLA outcomes for tickets raised from <c>from</c> (inclusive) to <c>to</c> (exclusive), UTC,
    /// grouped by <c>agent</c> or <c>priority</c>. One aggregation in the database.
    /// </summary>
    [HttpGet("sla-summary")]
    public Task<SlaSummaryDto> SlaSummary([FromQuery] SlaSummaryQuery query, CancellationToken ct) =>
        getSlaSummary.HandleAsync(query, ct);
}
