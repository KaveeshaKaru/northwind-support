using System.Net;
using System.Text.Json;

namespace SupportDesk.ApiTests;

/// <summary>The SLA summary over HTTP: query-string binding, validation and the seeded desk.</summary>
public sealed class ReportTests(SupportDeskApiFactory factory) : IClassFixture<SupportDeskApiFactory>
{
    [Theory]
    [InlineData("priority", 4)]
    // Six agents - Yuki is inactive now but still owns two older tickets - plus "Unassigned".
    [InlineData("agent", 7)]
    public async Task SlaSummary_OverTheWholeDesk_CountsEverySeededTicketOnce(string groupBy, int groups)
    {
        var client = await factory.SignedInClientAsync();

        var report = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync($"/api/reports/sla-summary?from=2000-01-01&to=2100-01-01&groupBy={groupBy}"));

        var rows = report.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(40, rows.Sum(row => row.GetProperty("total").GetInt32()));
        Assert.Equal(groups, rows.Count);
    }

    [Theory]
    [InlineData("from=2026-10-01&to=2026-09-01&groupBy=agent")]
    [InlineData("from=2026-10-01&to=2026-11-01")]
    [InlineData("from=2026-10-01&to=2026-11-01&groupBy=customer")]
    public async Task SlaSummary_WithABadQuery_IsABadRequest(string queryString)
    {
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/api/reports/sla-summary?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
