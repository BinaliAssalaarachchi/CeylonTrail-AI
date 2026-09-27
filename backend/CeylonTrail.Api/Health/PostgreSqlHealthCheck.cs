using CeylonTrail.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CeylonTrail.Api.Health;

public sealed class PostgreSqlHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unavailable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Database unavailable.");
        }
    }
}
