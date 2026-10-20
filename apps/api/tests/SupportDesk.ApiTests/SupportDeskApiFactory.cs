using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ApiTests;

/// <summary>
/// The API as it really runs, over an in-memory SQLite database that the real seeder fills -
/// so the seeded agents sign in with the development password, exactly as they do locally.
/// </summary>
public sealed class SupportDeskApiFactory : WebApplicationFactory<Program>
{
    public const string SaraEmail = "sara.lindqvist@northwind-support.example";

    // The in-memory database lives as long as this connection stays open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // Not Development, so the app does not try to run the SQL Server migrations.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "replaced-by-sqlite");
        builder.UseSetting("Jwt:SigningKey", "api-tests-only-signing-key-not-a-secret-0123456789");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        if (db.Database.EnsureCreated())
        {
            scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync().GetAwaiter().GetResult();
        }

        base.ConfigureClient(client);
    }

    /// <summary>A client signed in as the given seeded agent.</summary>
    public async Task<HttpClient> SignedInClientAsync(string email = SaraEmail)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, SupportDbSeeder.DevelopmentPassword));
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
