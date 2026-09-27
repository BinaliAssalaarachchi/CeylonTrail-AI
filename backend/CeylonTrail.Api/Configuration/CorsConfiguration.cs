using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CeylonTrail.Api.Configuration;

public static class CorsConfiguration
{
    public const string PolicyName = "ConfiguredOrigins";

    public static string[] GetAllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

    public static void AddConfiguredPolicy(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });
    }
}
