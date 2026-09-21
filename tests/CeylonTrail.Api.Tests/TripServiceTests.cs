using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Trips;
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
