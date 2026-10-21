using Microsoft.Extensions.Options;
using SupportDesk.Domain.Aggregates.Tickets.Triage;

namespace SupportDesk.Presentation.Extensions;

public static class SlaServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>Sla</c> configuration section and registers the SLA policy built from it.
    /// Invalid settings stop the application at startup rather than failing the first request.
    /// </summary>
    public static IServiceCollection AddSlaRules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SlaSettings>()
            .Bind(configuration.GetSection(SlaSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SlaSettings>, SlaSettingsValidator>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SlaSettings>>().Value);
        services.AddSingleton<SlaPolicy>();

        return services;
    }

    /// <summary>Reports every problem with the settings, so one restart is enough to fix them.</summary>
    private sealed class SlaSettingsValidator : IValidateOptions<SlaSettings>
    {
        public ValidateOptionsResult Validate(string? name, SlaSettings options)
        {
            var errors = options.Validate();

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
