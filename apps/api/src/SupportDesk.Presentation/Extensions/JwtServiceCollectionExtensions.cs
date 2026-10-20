using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using SupportDesk.Infrastructure.Security;

namespace SupportDesk.Presentation.Extensions;

public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>Jwt</c> section and makes bearer tokens the way callers authenticate. A
    /// missing or short signing key stops the application at startup.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((options, settings) =>
            {
                // Keep the token's own claim names (sub, email, name) rather than the long
                // WS-Federation URIs ASP.NET Core would otherwise translate them into.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = settings.ValidationParameters();
            });

        services.AddAuthorization();

        return services;
    }

    private sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
    {
        public ValidateOptionsResult Validate(string? name, JwtSettings options)
        {
            var errors = options.Validate();

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
