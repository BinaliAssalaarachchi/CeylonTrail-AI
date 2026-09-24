using CeylonTrail.Api.Configuration;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class PlannerConfigurationTests
{
    [Fact]
    public void PlannerDefaultsToTheDedicatedPlannerPort()
    {
        var options = new PlannerAgentOptions();

        Assert.Equal("http://localhost:8002", options.BaseUrl);
    }
}
