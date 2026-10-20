using Microsoft.OpenApi;

namespace SupportDesk.Presentation.Extensions;

public static class SwaggerServiceCollectionExtensions
{
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Swagger with an Authorize button: paste the token from POST /api/auth/login and every
    /// request from the UI carries it.
    /// </summary>
    public static IServiceCollection AddSwaggerWithBearerToken(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The token from POST /api/auth/login.",
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
        });
}
