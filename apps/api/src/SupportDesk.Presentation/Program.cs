using System.Text.Json.Serialization;
using SupportDesk.Application;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithBearerToken();

builder.Services.AddSlaRules(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Healthy only when the database answers, so a load balancer or orchestrator stops sending
// traffic to an API that cannot do its job.
builder.Services.AddHealthChecks().AddDbContextCheck<SupportDbContext>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Changing the database is a deliberate setting, not a side effect of the environment name: on
// in appsettings.Development.json and docker-compose.yml, so one command gives a working
// database; off by default, so a real deployment migrates as its own step.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
