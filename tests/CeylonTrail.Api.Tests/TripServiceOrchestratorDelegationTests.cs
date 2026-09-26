using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TripServiceOrchestratorDelegationTests
{
    [Fact]
    public async Task GenerateItinerary_WithOrchestrator_DelegatesBeforeLegacyPath()
    {
        await using var dbContext = CreateDbContext();
        var expected = new TripServiceResult<ItineraryResponse>(
            new ItineraryResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ItineraryStatus.Active,
                0m,
                DateTime.UtcNow,
                DateTime.UtcNow,
                []));
        var orchestrator = new RecordingOrchestrator(expected);
        var service = new TripService(dbContext, agentWorkflowOrchestrator: orchestrator);

        var result = await service.GenerateItineraryAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(orchestrator.WasCalled);
        Assert.Same(expected, result);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class RecordingOrchestrator(TripServiceResult<ItineraryResponse> result)
        : IAgentTripWorkflowOrchestrator
    {
        public bool WasCalled { get; private set; }

        public Task<TripServiceResult<ItineraryResponse>> ExecuteAsync(
            Guid touristId,
            Guid tripId,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
