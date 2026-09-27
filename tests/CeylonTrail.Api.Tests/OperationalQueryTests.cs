using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class OperationalQueryTests
{
    [Fact]
    public async Task StaffTripQueryFiltersSortsAndPaginatesServerSideContract()
    {
        await using var db = CreateDbContext();
        var tourist = new User { Id = Guid.NewGuid(), FirstName = "Asha", LastName = "Perera", Email = "asha@example.com" };
        db.Users.Add(tourist);
        db.Trips.AddRange(
            Trip(tourist, "Alpha trip", new DateOnly(2026, 10, 1), TripStatus.Planned),
            Trip(tourist, "Beta trip", new DateOnly(2026, 11, 1), TripStatus.Completed),
            Trip(tourist, "Gamma trip", new DateOnly(2026, 12, 1), TripStatus.Planned));
        await db.SaveChangesAsync();

        var result = await new TripService(db).GetStaffTripsPageAsync(new StaffTripQuery
        {
            Page = 2,
            PageSize = 1,
            Status = TripStatus.Planned,
            Search = "trip",
            SortBy = "startDate",
            SortDirection = "asc",
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
        Assert.Equal("Gamma trip", result.Items[0].Name);
    }

    [Fact]
    public async Task ProviderBookingQueryIsScopedAndSupportsFiltersAndSearch()
    {
        await using var db = CreateDbContext();
        var providerA = new User { Id = Guid.NewGuid(), FirstName = "Provider", LastName = "A", Email = "a@example.com", Role = UserRole.TourismProvider };
        var providerB = new User { Id = Guid.NewGuid(), FirstName = "Provider", LastName = "B", Email = "b@example.com", Role = UserRole.TourismProvider };
        var tourist = new User { Id = Guid.NewGuid(), FirstName = "Nimal", LastName = "Silva", Email = "nimal@example.com", Role = UserRole.Tourist };
        var category = new Category { Id = Guid.NewGuid(), Name = "Heritage" };
        var attractionA = Attraction(providerA, category, "Sigiriya");
        var attractionB = Attraction(providerB, category, "Ella");
        var slotA = Slot(attractionA);
        var slotB = Slot(attractionB);
        db.Users.AddRange(providerA, providerB, tourist);
        db.Categories.Add(category);
        db.Attractions.AddRange(attractionA, attractionB);
        db.AvailabilitySlots.AddRange(slotA, slotB);
        db.Bookings.AddRange(
            Booking(tourist, slotA, BookingStatus.PendingAI),
            Booking(tourist, slotB, BookingStatus.Confirmed));
        await db.SaveChangesAsync();

        var service = new BookingService(db);
        var result = await service.GetProviderBookingsPageAsync(
            providerA.Id,
            nameof(UserRole.TourismProvider),
            new BookingQuery { Search = "Sigiriya", Status = BookingStatus.PendingAI });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("PendingAI", result.Items[0].CurrentStatus);

        var legacyList = await service.GetProviderBookingsAsync(providerA.Id, nameof(UserRole.TourismProvider));
        Assert.Single(legacyList);
    }

    [Fact]
    public void QueryRejectsUnsupportedSortAndInvalidPageSize()
    {
        var tripQuery = new StaffTripQuery { SortBy = "touristPassword", PageSize = 101 };
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(tripQuery);
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(tripQuery, context, errors, true));
        Assert.NotEmpty(errors);
    }

    private static Trip Trip(User tourist, string name, DateOnly startDate, TripStatus status) => new()
    {
        Id = Guid.NewGuid(), TouristId = tourist.Id, Tourist = tourist, Name = name,
        StartDate = startDate, EndDate = startDate.AddDays(2), Budget = 100,
        Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static Attraction Attraction(User provider, Category category, string name) => new()
    {
        Id = Guid.NewGuid(), ProviderId = provider.Id, Provider = provider,
        CategoryId = category.Id, Category = category, Name = name, Status = "Approved",
        IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    private static AvailabilitySlot Slot(Attraction attraction) => new()
    {
        Id = Guid.NewGuid(), AttractionId = attraction.Id, Attraction = attraction,
        StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
        MaxCapacity = 10, PricePerPerson = 25,
    };

    private static Booking Booking(User tourist, AvailabilitySlot slot, BookingStatus status)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(), UserId = tourist.Id, User = tourist, CurrentStatus = status,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, TotalAmount = 25,
        };
        booking.Items.Add(new BookingItem
        {
            Id = Guid.NewGuid(), BookingId = booking.Id, Booking = booking,
            AvailabilitySlotId = slot.Id, AvailabilitySlot = slot, NumberOfGuests = 1,
            UnitPrice = 25, SubTotal = 25,
        });
        return booking;
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
