using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ApiTests;

/// <summary>AU-1, AU-4, AU-5, AU-6: signing in, and a valid token on every other endpoint.</summary>
public sealed class AuthenticationTests(SupportDeskApiFactory factory) : IClassFixture<SupportDeskApiFactory>
{
    public static TheoryData<string> ProtectedReads =>
    [
        "/api/tickets",
        "/api/tickets/1",
        "/api/tickets/1/escalations",
        "/api/agents",
        "/api/customers",
        "/api/customers/1",
        "/api/categories",
        "/api/auth/me",
    ];

    [Theory]
    [MemberData(nameof(ProtectedReads))]
    public async Task WithoutAToken_IsUnauthorized(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedReads))]
    public async Task WithAValidToken_Succeeds(string url)
    {
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WritesWithoutAToken_AreUnauthorized_BeforeTheBodyIsEvenValidated()
    {
        var client = factory.CreateClient();

        var escalate = await client.PostAsJsonAsync("/api/tickets/1/escalate", new { reason = "x" });
        var raise = await client.PostAsJsonAsync("/api/tickets", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, escalate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, raise.StatusCode);
    }

    [Fact]
    public void OnlyLoginAndHealth_AreOpenToAnonymousCallers()
    {
        // Guards the fallback policy: a new endpoint is protected unless someone opts it out.
        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        Assert.Equal(["/health", "api/auth/login"], anonymous.Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task Health_AnswersWithoutAToken_WhenTheDatabaseIsReachable()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ATamperedToken_IsUnauthorized()
    {
        var client = await factory.SignedInClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^2] + "xx");

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsAToken_ThatIdentifiesTheAgent()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(SupportDeskApiFactory.SaraEmail, SupportDbSeeder.DevelopmentPassword));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SupportDeskApiFactory.SaraEmail, login!.Agent.Email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var me = await client.GetFromJsonAsync<CurrentAgentDto>("/api/auth/me");
        Assert.Equal(login.Agent, me);
    }

    [Theory]
    [InlineData(SupportDeskApiFactory.SaraEmail, "wrong-password")]
    [InlineData("nobody@northwind-support.example", SupportDbSeeder.DevelopmentPassword)]
    public async Task Login_WithWrongCredentials_IsTheSame401WithNoToken(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("token", out _));
    }
}
