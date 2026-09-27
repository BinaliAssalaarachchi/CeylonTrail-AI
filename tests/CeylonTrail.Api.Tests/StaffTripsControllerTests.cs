using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.DTOs.Pagination;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class StaffTripsControllerTests
{
    [Fact]
    public void StaffTripRoutes_AllowOnlyTravelCoordinatorsAndAdministrators()
    {
        var authorization = typeof(StaffTripsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("TravelCoordinator,Administrator", authorization.Roles);
        Assert.DoesNotContain("Tourist", authorization.Roles);
        Assert.DoesNotContain("TourismProvider", authorization.Roles);
    }

    [Fact]
    public async Task GetAll_ReturnsStaffTripOverviewWithoutMutation()
    {
        var trip = new StaffTripResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Staff review trip",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 5),
            100000m,
            TripStatus.Planned,
            DateTime.UtcNow,
            DateTime.UtcNow,
            true);
        var service = new StubTripService { StaffTrips = new[] { trip } };
        var controller = new StaffTripsController(service);

        var result = await controller.GetAll(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var trips = Assert.IsAssignableFrom<IReadOnlyList<StaffTripResponse>>(response.Value);
        Assert.Single(trips);
        Assert.Equal(trip.Id, trips[0].Id);
        Assert.Equal(trip.TouristId, trips[0].TouristId);
        Assert.False(service.Mutated);
    }

    private sealed class StubTripService : ITripService
    {
        public IReadOnlyList<StaffTripResponse> StaffTrips { get; init; } = Array.Empty<StaffTripResponse>();

        public bool Mutated { get; private set; }

        public Task<IReadOnlyList<StaffTripResponse>> GetStaffTripsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(StaffTrips);

        public Task<PagedResponse<StaffTripResponse>> GetStaffTripsPageAsync(StaffTripQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResponse<StaffTripResponse>(StaffTrips, StaffTrips.Count, 1, 20, 1));

        public Task<TripServiceResult<StaffTripResponse>> GetStaffTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<StaffTripResponse>(NotFound: true));

        public Task<TripServiceResult<ItineraryResponse>> GetStaffItineraryAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<ItineraryResponse>(NotFound: true));

        public Task<TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetStaffItineraryHistoryAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(NotFound: true));

        public Task<TripServiceResult<ItineraryResponse>> GetStaffItineraryAsync(Guid tripId, Guid itineraryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<ItineraryResponse>(NotFound: true));

        public Task<TripServiceResult<TripResponse>> CreateTripAsync(Guid touristId, CreateTripRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<TripResponse>(Error: "Not used in this test."));

        public Task<IReadOnlyList<TripResponse>> GetTripsAsync(Guid touristId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TripResponse>>(Array.Empty<TripResponse>());

        public Task<TripServiceResult<TripResponse>> GetTripAsync(Guid touristId, Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<TripResponse>(NotFound: true));

        public Task<TripServiceResult<TripResponse>> UpdateTripAsync(Guid touristId, Guid tripId, UpdateTripRequest request, CancellationToken cancellationToken = default)
        {
            Mutated = true;
            return Task.FromResult(new TripServiceResult<TripResponse>(Error: "Mutation should not be called."));
        }

        public Task<bool> DeleteTripAsync(Guid touristId, Guid tripId, CancellationToken cancellationToken = default)
        {
            Mutated = true;
            return Task.FromResult(false);
        }

        public Task<TripServiceResult<TripPreferenceResponse>> AddPreferenceAsync(Guid touristId, Guid tripId, AddTripPreferenceRequest request, CancellationToken cancellationToken = default)
        {
            Mutated = true;
            return Task.FromResult(new TripServiceResult<TripPreferenceResponse>(Error: "Mutation should not be called."));
        }

        public Task<TripServiceResult<ItineraryResponse>> GetLatestItineraryAsync(Guid touristId, Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<ItineraryResponse>(NotFound: true));

        public Task<TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetItineraryHistoryAsync(Guid touristId, Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<IReadOnlyList<ItineraryHistoryItemResponse>>(NotFound: true));

        public Task<TripServiceResult<ItineraryResponse>> GetItineraryAsync(Guid touristId, Guid tripId, Guid itineraryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<ItineraryResponse>(NotFound: true));

        public Task<TripServiceResult<ItineraryResponse>> GenerateItineraryAsync(Guid touristId, Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripServiceResult<ItineraryResponse>(NotFound: true));
    }
}
