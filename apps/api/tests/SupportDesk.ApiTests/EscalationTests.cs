using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportDesk.ApiTests;

/// <summary>AU-8: an escalation is recorded against the signed-in agent, not a typed-in name.</summary>
public sealed class EscalationTests(SupportDeskApiFactory factory) : IClassFixture<SupportDeskApiFactory>
{
    [Fact]
    public async Task Escalating_RecordsTheAgentFromTheToken_AndIgnoresAnyNameInTheBody()
    {
        var client = await factory.SignedInClientAsync();
        var page = await client.GetFromJsonAsync<JsonElement>("/api/tickets?status=Open&priority=Low&pageSize=1");
        var ticketId = page.GetProperty("items")[0].GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/escalate",
            new { reason = "Customer's site is down.", escalatedBy = "someone-else" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/tickets/{ticketId}/escalations");
        Assert.Equal(SupportDeskApiFactory.SaraEmail, history[0].GetProperty("escalatedBy").GetString());
    }
}
