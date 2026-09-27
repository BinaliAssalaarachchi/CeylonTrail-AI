using CeylonTrail.Api.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class CorsConfigurationTests
{
    [Fact]
    public async Task ConfiguredOriginsAreAllowedAndUnconfiguredOriginsAreRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://example.com",
            })
            .Build();
        var services = new ServiceCollection();

        CorsConfiguration.AddConfiguredPolicy(services, configuration);

        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<ICorsPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(
            new DefaultHttpContext(),
            CorsConfiguration.PolicyName);

        Assert.NotNull(policy);
        Assert.Contains("https://example.com", policy!.Origins);
        Assert.DoesNotContain("https://unconfigured.example", policy.Origins);
        Assert.DoesNotContain("*", policy.Origins);
    }

    [Fact]
    public void MissingOriginsProduceNoPermissiveFallback()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Empty(CorsConfiguration.GetAllowedOrigins(configuration));
    }
}
