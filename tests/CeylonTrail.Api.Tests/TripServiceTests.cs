using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TripServiceTests
{
    [Fact]
    public async Task CreateTrip_WithValidRequest_CreatesDraftTrip()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();

        var before = DateTime.UtcNow;
        var result = await service.CreateTripAsync(touristId, ValidCreateRequest());
        var after = DateTime.UtcNow;

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(touristId, dbContext.Trips.Single().TouristId);
        Assert.Equal("Kandy Escape", result.Value!.Name);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Value.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 5), result.Value.EndDate);
        Assert.Equal(125000m, result.Value.Budget);
        Assert.Equal(TripStatus.Draft, result.Value.Status);
        Assert.InRange(result.Value.CreatedAt, before, after);
        Assert.InRange(result.Value.UpdatedAt, before, after);
    }

    [Fact]
    public async Task CreateTrip_WhenEndDatePrecedesStartDate_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var request = ValidCreateRequest();
        request.StartDate = new DateOnly(2026, 10, 5);
        request.EndDate = new DateOnly(2026, 10, 1);

        var result = await service.CreateTripAsync(Guid.NewGuid(), request);

        Assert.Equal("End date must be greater than or equal to start date.", result.Error);
        Assert.Empty(dbContext.Trips);
    }

    [Fact]
    public async Task CreateTrip_WithNegativeBudget_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var request = ValidCreateRequest();
        request.Budget = -1m;

        var result = await service.CreateTripAsync(Guid.NewGuid(), request);

        Assert.Equal("Budget must be greater than or equal to zero.", result.Error);
        Assert.Empty(dbContext.Trips);
    }

    [Fact]
    public async Task GetTrips_ReturnsOnlyTripsOwnedByRequestedTourist()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristA = Guid.NewGuid();
        var touristB = Guid.NewGuid();
        await SeedTripAsync(dbContext, touristA, "A's trip");
        await SeedTripAsync(dbContext, touristB, "B's trip");

        var trips = await service.GetTripsAsync(touristB);

        var trip = Assert.Single(trips);
        Assert.Equal("B's trip", trip.Name);
    }

    [Fact]
    public async Task OtherTourist_CannotGetUpdateDeleteOrAddPreferenceToTrip()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var ownerId = Guid.NewGuid();
        var otherTouristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, ownerId, "Private trip");

        var getResult = await service.GetTripAsync(otherTouristId, trip.Id);
        var updateResult = await service.UpdateTripAsync(otherTouristId, trip.Id, ValidUpdateRequest());
        var deleteResult = await service.DeleteTripAsync(otherTouristId, trip.Id);
        var preferenceResult = await service.AddPreferenceAsync(
            otherTouristId,
            trip.Id,
            new AddTripPreferenceRequest { PreferenceType = "Interest", Value = "Wildlife" });

        Assert.True(getResult.NotFound);
        Assert.True(updateResult.NotFound);
        Assert.False(deleteResult);
        Assert.True(preferenceResult.NotFound);
        Assert.NotNull(await dbContext.Trips.FindAsync(trip.Id));
        Assert.Empty(dbContext.TripPreferences);
    }

    [Fact]
    public async Task UpdateTrip_WithValidRequest_PersistsChangedValues()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Original trip");
        var request = new UpdateTripRequest
        {
            Name = "Updated trip",
            StartDate = new DateOnly(2026, 11, 2),
            EndDate = new DateOnly(2026, 11, 8),
            Budget = 220000m
        };

        var result = await service.UpdateTripAsync(touristId, trip.Id, request);
        var saved = await dbContext.Trips.SingleAsync(candidate => candidate.Id == trip.Id);

        Assert.Null(result.Error);
        Assert.Equal("Updated trip", saved.Name);
        Assert.Equal(request.StartDate, saved.StartDate);
        Assert.Equal(request.EndDate, saved.EndDate);
        Assert.Equal(request.Budget, saved.Budget);
        Assert.Equal(TripStatus.Draft, saved.Status);
    }

    [Theory]
    [InlineData(TripStatus.Draft, TripStatus.Planned)]
    [InlineData(TripStatus.Draft, TripStatus.Cancelled)]
    [InlineData(TripStatus.Planned, TripStatus.Completed)]
    [InlineData(TripStatus.Planned, TripStatus.Cancelled)]
    public async Task UpdateTrip_AllowsValidStatusTransitions(TripStatus current, TripStatus requested)
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Status trip", current);

        var request = ValidUpdateRequest();
        request.Status = requested;
        var result = await service.UpdateTripAsync(touristId, trip.Id, request);

        Assert.Null(result.Error);
        Assert.Equal(requested, result.Value!.Status);
    }

    [Theory]
    [InlineData(TripStatus.Completed, TripStatus.Draft)]
    [InlineData(TripStatus.Cancelled, TripStatus.Planned)]
    [InlineData(TripStatus.Planned, TripStatus.Draft)]
    public async Task UpdateTrip_RejectsInvalidStatusTransitions(TripStatus current, TripStatus requested)
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Status trip", current);

        var request = ValidUpdateRequest();
        request.Status = requested;
        var result = await service.UpdateTripAsync(touristId, trip.Id, request);

        Assert.Contains($"cannot transition from {current} to {requested}", result.Error);
        Assert.Equal(current, (await dbContext.Trips.FindAsync(trip.Id))!.Status);
    }

    [Fact]
    public async Task DeleteTrip_ByOwner_RemovesTrip()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Trip to delete");

        Assert.True(await service.DeleteTripAsync(touristId, trip.Id));

        Assert.True((await service.GetTripAsync(touristId, trip.Id)).NotFound);
        Assert.Null(await dbContext.Trips.FindAsync(trip.Id));
    }

    [Fact]
    public async Task DeleteTrip_WithPersistedWorkflowReturnsConflictAndPreservesTrip()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Workflow trip");
        dbContext.AgentWorkflows.Add(new AgentWorkflow
        {
            Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = trip.Id,
            RequestedByUserId = touristId, StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await new TripService(dbContext).DeleteTripWithResultAsync(touristId, trip.Id);

        Assert.True(result.Conflict);
        Assert.False(result.Value);
        Assert.NotNull(await dbContext.Trips.FindAsync(trip.Id));
        Assert.False(await new TripService(dbContext).DeleteTripAsync(touristId, trip.Id));
    }

    [Fact]
    public async Task AddPreference_ByOwner_CreatesPreference()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Preference trip");

        var result = await service.AddPreferenceAsync(
            touristId,
            trip.Id,
            new AddTripPreferenceRequest { PreferenceType = "Interest", Value = "Wildlife" });

        Assert.Null(result.Error);
        Assert.Equal("Interest", result.Value!.PreferenceType);
        Assert.Equal("Wildlife", result.Value.Value);
        Assert.Equal(trip.Id, dbContext.TripPreferences.Single().TripId);
    }

    [Theory]
    [InlineData("", "Wildlife")]
    [InlineData("Interest", "")]
    public async Task AddPreference_WithEmptyValue_IsRejected(string preferenceType, string value)
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Preference trip");

        var result = await service.AddPreferenceAsync(
            touristId,
            trip.Id,
            new AddTripPreferenceRequest { PreferenceType = preferenceType, Value = value });

        Assert.Equal("Preference type and value are required.", result.Error);
        Assert.Empty(dbContext.TripPreferences);
    }

    [Fact]
    public async Task GetLatestItinerary_WithNoItinerary_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "No itinerary trip");

        var result = await service.GetLatestItineraryAsync(touristId, trip.Id);

        Assert.True(result.NotFound);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetLatestItinerary_MapsLatestItineraryAndNestedItems()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Itinerary trip");
        var older = new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Superseded,
            TotalEstimatedCost = 100m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var latest = new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = 250m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Days =
            {
                new ItineraryDay
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 1,
                    Date = new DateOnly(2026, 10, 1),
                    Items =
                    {
                        new ItineraryItem
                        {
                            Id = Guid.NewGuid(),
                            AttractionId = Guid.NewGuid(),
                            StartTime = new TimeOnly(9, 0),
                            EndTime = new TimeOnly(11, 0),
                            EstimatedCost = 35m,
                            Notes = "Morning visit"
                        }
                    }
                }
            }
        };
        dbContext.Itineraries.AddRange(older, latest);
        await dbContext.SaveChangesAsync();

        var result = await service.GetLatestItineraryAsync(touristId, trip.Id);

        Assert.Null(result.Error);
        Assert.Equal(latest.Id, result.Value!.Id);
        Assert.Equal(ItineraryStatus.Active, result.Value.Status);
        var day = Assert.Single(result.Value.Days);
        var item = Assert.Single(day.Items);
        Assert.Equal(1, day.DayNumber);
        Assert.Equal(35m, item.EstimatedCost);
        Assert.Equal("Morning visit", item.Notes);
    }

    [Fact]
    public async Task OtherTourist_CannotReadTripItinerary()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var ownerId = Guid.NewGuid();
        var otherTouristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, ownerId, "Private itinerary trip");
        dbContext.Itineraries.Add(new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = 50m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetLatestItineraryAsync(otherTouristId, trip.Id);

        Assert.True(result.NotFound);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ItineraryHistory_ReturnsOwnVersionsNewestFirst_AndIndividualVersionsAreScoped()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, ownerId, "History trip");
        var otherTrip = await SeedTripAsync(dbContext, otherOwnerId, "Other history trip");
        var older = new Itinerary { Id = Guid.NewGuid(), TripId = trip.Id, Status = ItineraryStatus.Superseded, TotalEstimatedCost = 52000m, CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1) };
        var latest = new Itinerary { Id = Guid.NewGuid(), TripId = trip.Id, Status = ItineraryStatus.Active, TotalEstimatedCost = 48000m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var foreign = new Itinerary { Id = Guid.NewGuid(), TripId = otherTrip.Id, Status = ItineraryStatus.Active, TotalEstimatedCost = 1m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.Itineraries.AddRange(older, latest, foreign);
        await dbContext.SaveChangesAsync();

        var history = await service.GetItineraryHistoryAsync(ownerId, trip.Id);
        Assert.False(history.NotFound);
        var historyItems = history.Value ?? throw new InvalidOperationException("History was not returned.");
        Assert.Equal(new[] { latest.Id, older.Id }, historyItems.Select(item => item.Id));
        Assert.Equal(new[] { ItineraryStatus.Active, ItineraryStatus.Superseded }, historyItems.Select(item => item.Status));
        Assert.Equal(2, historyItems.Count);

        Assert.False((await service.GetItineraryAsync(ownerId, trip.Id, older.Id)).NotFound);
        Assert.True((await service.GetItineraryAsync(otherOwnerId, trip.Id, latest.Id)).NotFound);
        Assert.True((await service.GetItineraryAsync(ownerId, trip.Id, foreign.Id)).NotFound);
        Assert.True((await service.GetItineraryHistoryAsync(ownerId, Guid.NewGuid())).NotFound);
    }

    [Fact]
    public async Task ResourceOperations_WithEmptyOrMissingTripId_ReturnNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        Assert.True((await service.GetTripAsync(touristId, Guid.Empty)).NotFound);
        Assert.True((await service.UpdateTripAsync(touristId, Guid.Empty, ValidUpdateRequest())).NotFound);
        Assert.False(await service.DeleteTripAsync(touristId, Guid.Empty));
        Assert.True((await service.AddPreferenceAsync(
            touristId,
            missingId,
            new AddTripPreferenceRequest { PreferenceType = "Interest", Value = "Wildlife" })).NotFound);
        Assert.True((await service.GetLatestItineraryAsync(touristId, missingId)).NotFound);
        Assert.True((await service.GenerateItineraryAsync(touristId, Guid.Empty)).NotFound);
    }

    [Fact]
    public async Task GetStaffTrips_ReturnsTripsWithItineraryAvailabilityWithoutChangingOwnership()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Coordinator view");
        dbContext.Itineraries.Add(new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = 75m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var trips = await service.GetStaffTripsAsync();

        var staffTrip = Assert.Single(trips);
        Assert.Equal(touristId, staffTrip.TouristId);
        Assert.True(staffTrip.HasItinerary);
        Assert.Equal(touristId, (await dbContext.Trips.FindAsync(trip.Id))!.TouristId);
    }

    [Fact]
    public async Task GetStaffItinerary_ReturnsItineraryWithoutTouristOwnershipFilter()
    {
        await using var dbContext = CreateDbContext();
        var service = new TripService(dbContext);
        var trip = await SeedTripAsync(dbContext, Guid.NewGuid(), "Coordinator itinerary view");
        var itinerary = new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Generated,
            TotalEstimatedCost = 125m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Itineraries.Add(itinerary);
        await dbContext.SaveChangesAsync();

        var result = await service.GetStaffItineraryAsync(trip.Id);

        Assert.False(result.NotFound);
        Assert.Equal(itinerary.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GenerateItinerary_ByOwner_PersistsHierarchyAndPlansDraftTrip()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Planner trip");
        var attraction = await SeedApprovedAttractionAsync(dbContext, "Temple", 2500m);
        var planner = new StubPlannerAgent(_ => GeneratedPlan(attraction.Id, trip.StartDate));
        var service = new TripService(dbContext, planner, new AttractionService(dbContext));

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Null(result.Error);
        Assert.Equal(TripStatus.Planned, (await dbContext.Trips.FindAsync(trip.Id))!.Status);
        var saved = Assert.Single(dbContext.Itineraries.Include(item => item.Days).ThenInclude(day => day.Items));
        Assert.Equal(ItineraryStatus.Active, saved.Status);
        Assert.Equal(2500m, saved.TotalEstimatedCost);
        Assert.Equal(attraction.Id, Assert.Single(Assert.Single(saved.Days).Items).AttractionId);
    }

    [Fact]
    public async Task GenerateItinerary_ByAnotherTourist_ReturnsNotFoundWithoutPersistence()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, ownerId, "Private planner trip");
        var service = new TripService(dbContext, new StubPlannerAgent(_ => throw new InvalidOperationException()));

        var result = await service.GenerateItineraryAsync(Guid.NewGuid(), trip.Id);

        Assert.True(result.NotFound);
        Assert.Empty(dbContext.Itineraries);
    }

    [Theory]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Cancelled)]
    public async Task GenerateItinerary_TerminalTrip_IsRejected(TripStatus status)
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Terminal planner trip", status);
        var service = new TripService(dbContext, new StubPlannerAgent(_ => throw new InvalidOperationException()));

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Contains($"{status} trip cannot", result.Error);
        Assert.Empty(dbContext.Itineraries);
    }

    [Fact]
    public async Task GenerateItinerary_WhenAgentFails_DoesNotPersistOrChangeTrip()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Failed planner trip");
        await SeedApprovedAttractionAsync(dbContext, "Museum", 1000m);
        var service = new TripService(dbContext, new StubPlannerAgent(_ => throw new InvalidOperationException("agent failure")), new AttractionService(dbContext));

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Contains("Planner Agent failed", result.Error);
        Assert.Empty(dbContext.Itineraries);
        Assert.Equal(TripStatus.Draft, (await dbContext.Trips.FindAsync(trip.Id))!.Status);
    }

    [Fact]
    public async Task GenerateItinerary_RegenerationSupersedesPreviousActiveItinerary()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Regeneration trip");
        var attraction = await SeedApprovedAttractionAsync(dbContext, "Fort", 1500m);
        var previous = new Itinerary { Id = Guid.NewGuid(), TripId = trip.Id, Status = ItineraryStatus.Active, TotalEstimatedCost = 1500m, CreatedAt = DateTime.UtcNow.AddMinutes(-1), UpdatedAt = DateTime.UtcNow.AddMinutes(-1) };
        dbContext.Itineraries.Add(previous);
        await dbContext.SaveChangesAsync();
        var service = new TripService(dbContext, new StubPlannerAgent(_ => GeneratedPlan(attraction.Id, trip.StartDate, 1500m)), new AttractionService(dbContext));

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Null(result.Error);
        Assert.Equal(ItineraryStatus.Superseded, (await dbContext.Itineraries.FindAsync(previous.Id))!.Status);
        Assert.Equal(ItineraryStatus.Active, (await dbContext.Itineraries.SingleAsync(item => item.Id != previous.Id)).Status);
    }

    [Fact]
    public async Task GenerateItinerary_InvalidPlannerAttraction_IsRejectedWithoutPersistence()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId, "Invalid planner trip");
        await SeedApprovedAttractionAsync(dbContext, "Gallery", 1000m);
        var service = new TripService(dbContext, new StubPlannerAgent(_ => GeneratedPlan(Guid.NewGuid(), trip.StartDate)), new AttractionService(dbContext));

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Contains("unknown attraction", result.Error);
        Assert.Empty(dbContext.Itineraries);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Trip> SeedTripAsync(
        ApplicationDbContext dbContext,
        Guid touristId,
        string name,
        TripStatus status = TripStatus.Draft)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            TouristId = touristId,
            Name = name,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 5),
            Budget = 100000m,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Trips.Add(trip);
        await dbContext.SaveChangesAsync();
        return trip;
    }

    private static async Task<Attraction> SeedApprovedAttractionAsync(
        ApplicationDbContext dbContext,
        string name,
        decimal price)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Culture" };
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CategoryId = category.Id,
            Category = category, Name = name, Description = "A supplied candidate.",
            District = "Kandy", Address = "Central Sri Lanka", Price = price,
            Status = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        dbContext.Attractions.Add(attraction);
        await dbContext.SaveChangesAsync();
        return attraction;
    }

    private static PlannerAgentResponse GeneratedPlan(Guid attractionId, DateOnly date, decimal cost = 2500m) => new(
        new[] { new PlannerDay(1, date, new[] { new PlannerItem(attractionId, new TimeOnly(9), new TimeOnly(11), cost, "Supplied candidate") }) },
        cost,
        "Generated",
        null);

    private sealed class StubPlannerAgent(Func<PlannerAgentRequest, PlannerAgentResponse> generate) : IPlannerAgentService
    {
        public Task<PlannerAgentServiceResult> GenerateAsync(PlannerAgentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlannerAgentServiceResult(Value: generate(request)));
    }

    private static CreateTripRequest ValidCreateRequest() => new()
    {
        Name = "Kandy Escape",
        StartDate = new DateOnly(2026, 10, 1),
        EndDate = new DateOnly(2026, 10, 5),
        Budget = 125000m
    };

    private static UpdateTripRequest ValidUpdateRequest() => new()
    {
        Name = "Updated trip",
        StartDate = new DateOnly(2026, 10, 2),
        EndDate = new DateOnly(2026, 10, 6),
        Budget = 150000m
    };
}
