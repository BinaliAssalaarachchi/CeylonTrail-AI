using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task CreateBooking_CalculatesTotalAndCreatesStatusHistory()
    {
        await using var dbContext = CreateDbContext();
        var slot1 = CreateSlot(10, 50.00m);
        var slot2 = CreateSlot(10, 30.00m);
        dbContext.AvailabilitySlots.AddRange(slot1, slot2);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();

        var request = new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot1.Id, NumberOfGuests = 2, UnitPrice = 50.00m },
                new() { AvailabilitySlotId = slot2.Id, NumberOfGuests = 1, UnitPrice = 30.00m }
            }
        };

        var result = await service.CreateBookingAsync(touristId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(130.00m, result.Response!.TotalAmount);
        Assert.Equal("Draft", result.Response.CurrentStatus);
        Assert.Equal(2, result.Response.Items.Count);
        Assert.Single(result.Response.StatusHistory!);
        Assert.Equal("Draft", result.Response.StatusHistory![0].NewStatus);
    }

    [Fact]
    public async Task BookingAccess_IsScopedToTouristAndReferencedAttractionProvider()
    {
        await using var dbContext = CreateDbContext();
        var ownerProviderId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var touristId = Guid.NewGuid();
        var slot = CreateSlot(10, 50m);
        slot.Attraction!.ProviderId = ownerProviderId;
        var booking = new Booking
        {
            Id = Guid.NewGuid(), UserId = touristId, CurrentStatus = BookingStatus.Draft,
            TotalAmount = 50m, Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 50m, SubTotal = 50m }
            }
        };
        dbContext.AvailabilitySlots.Add(slot);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        var service = new BookingService(dbContext);

        Assert.True((await service.GetBookingByIdAsync(booking.Id, ownerProviderId, nameof(UserRole.TourismProvider))).Succeeded);
        Assert.False((await service.GetBookingByIdAsync(booking.Id, otherProviderId, nameof(UserRole.TourismProvider))).Succeeded);
        Assert.False((await service.GetBookingHistoryAsync(booking.Id, otherProviderId, nameof(UserRole.TourismProvider))).Succeeded);
        Assert.False((await service.AcceptBookingAsync(booking.Id, otherProviderId, nameof(UserRole.TourismProvider))).Succeeded);
        Assert.False((await service.RejectBookingAsync(booking.Id, otherProviderId, new RejectBookingRequest { Reason = "No access" }, nameof(UserRole.TourismProvider))).Succeeded);
        Assert.False((await service.AcceptBookingAsync(booking.Id, touristId, nameof(UserRole.Tourist))).Succeeded);
        Assert.True((await service.AcceptBookingAsync(booking.Id, ownerProviderId, nameof(UserRole.TourismProvider))).Succeeded);
    }

    [Fact]
    public async Task Provider_CannotManageBookingContainingAnotherProvidersItem()
    {
        await using var dbContext = CreateDbContext();
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();
        var tourist = Guid.NewGuid();
        var first = CreateSlot(10, 50m);
        var second = CreateSlot(10, 75m);
        first.Attraction!.ProviderId = providerA;
        second.Attraction!.ProviderId = providerB;
        dbContext.AvailabilitySlots.AddRange(first, second);
        var booking = new Booking
        {
            Id = Guid.NewGuid(), UserId = tourist, CurrentStatus = BookingStatus.Draft,
            Items =
            [
                new() { Id = Guid.NewGuid(), AvailabilitySlotId = first.Id, NumberOfGuests = 1, UnitPrice = 50m, SubTotal = 50m },
                new() { Id = Guid.NewGuid(), AvailabilitySlotId = second.Id, NumberOfGuests = 1, UnitPrice = 75m, SubTotal = 75m }
            ],
            TotalAmount = 125m
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).AcceptBookingAsync(
            booking.Id, providerA, nameof(UserRole.TourismProvider));

        Assert.False(result.Succeeded);
        Assert.Equal(BookingStatus.Draft, booking.CurrentStatus);
    }

    [Fact]
    public async Task Provider_CannotCancelOrDeleteTouristBooking()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();
        var tourist = Guid.NewGuid();
        var provider = Guid.NewGuid();
        var service = new BookingService(dbContext);
        var created = await service.CreateBookingAsync(tourist, new CreateBookingRequest
        {
            Items = [new BookingItemRequest { AvailabilitySlotId = slot.Id, NumberOfGuests = 1 }]
        });

        var cancel = await service.CancelBookingAsync(
            created.Response!.Id, provider, nameof(UserRole.TourismProvider),
            new CancelBookingRequest { Reason = "Not permitted" });
        var delete = await service.DeleteBookingAsync(
            created.Response.Id, provider, nameof(UserRole.TourismProvider));

        Assert.False(cancel.Succeeded);
        Assert.False(delete.Succeeded);
        Assert.NotNull(await dbContext.Bookings.FindAsync(created.Response.Id));
    }

    [Fact]
    public async Task CreateBooking_RejectsDuplicateAvailabilitySlots()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            Items =
            [
                new BookingItemRequest { AvailabilitySlotId = slot.Id, NumberOfGuests = 1 },
                new BookingItemRequest { AvailabilitySlotId = slot.Id, NumberOfGuests = 1 }
            ]
        });

        Assert.False(result.Succeeded);
        Assert.Contains("unique availability slots", result.Error);
    }

    [Fact]
    public async Task CreateAvailabilitySlot_RejectsProviderForAnotherProvidersAttraction()
    {
        await using var dbContext = CreateDbContext();
        var ownerProviderId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var attractionId = Guid.NewGuid();
        dbContext.Attractions.Add(new Attraction
        {
            Id = attractionId, ProviderId = ownerProviderId, CategoryId = Guid.NewGuid(),
            Name = "Owned attraction", Status = "Approved", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateAvailabilitySlotAsync(
            new CreateAvailabilitySlotRequest
            {
                AttractionId = attractionId,
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
                MaxCapacity = 5,
                PricePerPerson = 25m
            },
            otherProviderId,
            nameof(UserRole.TourismProvider));

        Assert.False(result.Succeeded);
        Assert.Equal("Attraction not found.", result.Error);
        Assert.Empty(dbContext.AvailabilitySlots);
    }

    [Fact]
    public async Task CreateBooking_WithOwnTrip_SucceedsAndPersistsTripOwnership()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = CreateTrip(touristId);
        var slot = CreateSlot(10, 50m);
        dbContext.Trips.Add(trip);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(touristId, new CreateBookingRequest
        {
            TripId = trip.Id,
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 0.01m }
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal(trip.Id, result.Response!.TripId);
        Assert.Equal(50m, result.Response.TotalAmount);
    }

    [Fact]
    public async Task CreateBooking_WithAnotherTouristsTrip_ReturnsSafeNotFound()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var trip = CreateTrip(ownerId);
        var slot = CreateSlot(10, 50m);
        dbContext.Trips.Add(trip);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(requesterId, new CreateBookingRequest
        {
            TripId = trip.Id,
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 50m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Trip not found.", result.Error);
        Assert.Empty(dbContext.Bookings);
    }

    [Fact]
    public async Task CreateBooking_WithNonexistentTrip_ReturnsSafeNotFound()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            TripId = Guid.NewGuid(),
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 50m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Trip not found.", result.Error);
    }

    [Fact]
    public async Task CreateBooking_WithInvalidGuestCount_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 0, UnitPrice = 50m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("NumberOfGuests must be greater than zero.", result.Error);
    }

    [Fact]
    public async Task CreateBooking_WithUnbookableAttraction_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        slot.Attraction!.Status = "PendingApproval";
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 50m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("The attraction is not available for booking.", result.Error);
    }

    [Fact]
    public async Task CreateBooking_WhenCapacityIsInsufficient_ReturnsErrorWithoutBooking()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(1, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 2, UnitPrice = 50m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Contains("insufficient capacity", result.Error);
        Assert.Empty(dbContext.Bookings);
        Assert.Equal(0, slot.BookedCapacity);
    }

    [Fact]
    public async Task GetAvailabilitySlots_ReturnsOnlyBookableSlots()
    {
        await using var dbContext = CreateDbContext();
        var bookable = CreateSlot(10, 50m);
        var full = CreateSlot(10, 50m);
        full.BookedCapacity = full.MaxCapacity;
        var expired = CreateSlot(10, 50m);
        expired.EndTime = DateTime.UtcNow.AddMinutes(-1);
        var pending = CreateSlot(10, 50m);
        pending.Attraction!.Status = "PendingApproval";
        var lowerCaseApproved = CreateSlot(10, 50m);
        lowerCaseApproved.Attraction!.Status = "approved";
        dbContext.AvailabilitySlots.AddRange(bookable, full, expired, pending);
        dbContext.AvailabilitySlots.Add(lowerCaseApproved);
        await dbContext.SaveChangesAsync();

        var result = await new BookingService(dbContext).GetAvailabilitySlotsAsync();

        var returnedIds = result.Select(slot => slot.Id).ToList();
        Assert.Contains(bookable.Id, returnedIds);
        Assert.DoesNotContain(full.Id, returnedIds);
        Assert.DoesNotContain(expired.Id, returnedIds);
        Assert.DoesNotContain(pending.Id, returnedIds);
        Assert.DoesNotContain(lowerCaseApproved.Id, returnedIds);
        Assert.Equal(bookable.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task CreateBooking_WithEmptyItems_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);

        var result = await service.CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest { Items = new() });

        Assert.False(result.Succeeded);
        Assert.Equal("A booking must contain at least one item.", result.Error);
    }

    [Fact]
    public async Task AcceptBooking_WhenDraftOrPending_TransitionsToConfirmedAndDeductsCapacity()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 100m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 3, UnitPrice = 100m }
            }
        });

        // Verify slot capacity is NOT deducted while in Draft
        var dbSlotBeforeAccept = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(0, dbSlotBeforeAccept!.BookedCapacity);
        Assert.Equal(10, dbSlotBeforeAccept.AvailableCapacity);

        var bookingId = createResult.Response!.Id;

        var acceptResult = await service.AcceptBookingAsync(bookingId, providerId);

        Assert.True(acceptResult.Succeeded);
        Assert.Equal("Confirmed", acceptResult.Response!.CurrentStatus);
        Assert.Equal(2, acceptResult.Response.StatusHistory!.Count);
        Assert.Equal("Confirmed", acceptResult.Response.StatusHistory[1].NewStatus);

        // Verify slot capacity IS deducted after confirmation
        var dbSlotAfterAccept = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(3, dbSlotAfterAccept!.BookedCapacity);
        Assert.Equal(7, dbSlotAfterAccept.AvailableCapacity);
    }

    [Fact]
    public async Task AcceptBooking_WhenAlreadyConfirmed_PreventsInvalidStatusTransition()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 100m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 100m }
            }
        });

        var bookingId = createResult.Response!.Id;

        // First accept -> Confirmed
        await service.AcceptBookingAsync(bookingId, providerId);

        // Second accept attempt -> Should fail
        var secondAccept = await service.AcceptBookingAsync(bookingId, providerId);

        Assert.False(secondAccept.Succeeded);
        Assert.Contains("Cannot accept booking in 'Confirmed' status", secondAccept.Error);
    }

    [Fact]
    public async Task RejectBooking_TransitionsToRejectedWithReason()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 50m }
            }
        });

        var bookingId = createResult.Response!.Id;
        const string reason = "Attraction is fully booked for this time slot.";

        var rejectResult = await service.RejectBookingAsync(bookingId, providerId, new RejectBookingRequest { Reason = reason });

        Assert.True(rejectResult.Succeeded);
        Assert.Equal("Rejected", rejectResult.Response!.CurrentStatus);
        Assert.Equal(reason, rejectResult.Response.StatusHistory![1].Reason);
    }

    [Fact]
    public async Task CancelBooking_ByOwnerTourist_SucceedsAndCreatesCancellationRecord()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 75m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 75m }
            }
        });

        var bookingId = createResult.Response!.Id;
        const string reason = "Change of travel plans.";

        var cancelResult = await service.CancelBookingAsync(
            bookingId,
            touristId,
            nameof(UserRole.Tourist),
            new CancelBookingRequest { Reason = reason });

        Assert.True(cancelResult.Succeeded);
        Assert.Equal("Cancelled", cancelResult.Response!.CurrentStatus);
        Assert.NotNull(cancelResult.Response.CancellationRequests);
        Assert.Single(cancelResult.Response.CancellationRequests!);
        Assert.Equal(reason, cancelResult.Response.CancellationRequests![0].Reason);
    }

    [Fact]
    public async Task CancelBooking_ByAnotherTourist_FailsWithOwnershipError()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 100m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristA = Guid.NewGuid();
        var touristB = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristA, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 1, UnitPrice = 100m }
            }
        });

        var bookingId = createResult.Response!.Id;

        var cancelResult = await service.CancelBookingAsync(
            bookingId,
            touristB,
            nameof(UserRole.Tourist),
            new CancelBookingRequest { Reason = "Malicious cancellation attempt" });

        Assert.False(cancelResult.Succeeded);
        Assert.Equal("You cannot cancel another user's booking.", cancelResult.Error);
    }

    [Fact]
    public async Task CancelBooking_WhenConfirmed_RestoresCapacity()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 75m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 4, UnitPrice = 75m }
            }
        });

        var bookingId = createResult.Response!.Id;
        await service.AcceptBookingAsync(bookingId, providerId);

        var dbSlotAfterAccept = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(4, dbSlotAfterAccept!.BookedCapacity);
        Assert.Equal(6, dbSlotAfterAccept.AvailableCapacity);

        var cancelResult = await service.CancelBookingAsync(
            bookingId,
            touristId,
            nameof(UserRole.Tourist),
            new CancelBookingRequest { Reason = "Change of plans" });

        Assert.True(cancelResult.Succeeded);
        var dbSlotAfterCancel = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(0, dbSlotAfterCancel!.BookedCapacity);
        Assert.Equal(10, dbSlotAfterCancel.AvailableCapacity);
    }

    [Fact]
    public async Task RejectBooking_WhenConfirmed_RestoresCapacity()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50m);
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 2, UnitPrice = 50m }
            }
        });

        var bookingId = createResult.Response!.Id;
        await service.AcceptBookingAsync(bookingId, providerId);

        var dbSlotAfterAccept = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(2, dbSlotAfterAccept!.BookedCapacity);
        Assert.Equal(8, dbSlotAfterAccept.AvailableCapacity);

        var rejectResult = await service.RejectBookingAsync(
            bookingId,
            providerId,
            new RejectBookingRequest { Reason = "Emergency closure" });

        Assert.True(rejectResult.Succeeded);
        var dbSlotAfterReject = await dbContext.AvailabilitySlots.FindAsync(slot.Id);
        Assert.Equal(0, dbSlotAfterReject!.BookedCapacity);
        Assert.Equal(10, dbSlotAfterReject.AvailableCapacity);
    }

    [Fact]
    public async Task CreateBooking_WhenActiveAdvisoryExistsForSlotDateAndDistrict_IncludesAdvisoryInResponse()
    {
        await using var dbContext = CreateDbContext();
        var slot = CreateSlot(10, 50.00m);
        var alert = new TravelAlert
        {
            Id = Guid.NewGuid(),
            Title = "Heavy Monsoon Rain",
            Description = "Flash flooding warning in Kandy",
            AlertType = TravelAlertType.Weather,
            Severity = TravelAlertSeverity.High,
            District = "Kandy",
            StartDateTime = DateTime.UtcNow.AddHours(-1),
            EndDateTime = DateTime.UtcNow.AddHours(5),
            Status = TravelAlertStatus.Active,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.AvailabilitySlots.Add(slot);
        dbContext.TravelAlerts.Add(alert);
        await dbContext.SaveChangesAsync();

        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();

        var request = new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AvailabilitySlotId = slot.Id, NumberOfGuests = 2, UnitPrice = 50.00m }
            }
        };

        var result = await service.CreateBookingAsync(touristId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.NotNull(result.Response!.ActiveAdvisories);
        Assert.Single(result.Response.ActiveAdvisories!);
        Assert.Equal("Heavy Monsoon Rain", result.Response.ActiveAdvisories![0].Title);
        Assert.Equal(TravelAlertSeverity.High, result.Response.ActiveAdvisories![0].Severity);
    }

    private static AvailabilitySlot CreateSlot(int capacity, decimal price)
    {
        var attractionId = Guid.NewGuid();
        return new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            AttractionId = attractionId,
            Attraction = new Attraction
            {
                Id = attractionId,
                ProviderId = Guid.NewGuid(),
                CategoryId = Guid.NewGuid(),
                Name = "Test attraction",
                Description = "Test attraction",
                District = "Kandy",
                Address = "Test address",
                Status = "Approved",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            MaxCapacity = capacity,
            BookedCapacity = 0,
            PricePerPerson = price,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Trip CreateTrip(Guid touristId) => new()
    {
        Id = Guid.NewGuid(),
        TouristId = touristId,
        Name = "Test trip",
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
        Budget = 1000m,
        Status = TripStatus.Draft,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
