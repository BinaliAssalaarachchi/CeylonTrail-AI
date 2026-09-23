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
        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();

        var request = new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 2, UnitPrice = 50.00m },
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 30.00m }
            }
        };

        var result = await service.CreateBookingAsync(touristId, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(130.00m, result.Response!.TotalAmount);
        Assert.Equal("Pending", result.Response.Status);
        Assert.Equal(2, result.Response.Items.Count);
        Assert.Single(result.Response.StatusHistory!);
        Assert.Equal("Pending", result.Response.StatusHistory![0].NewStatus);
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
    public async Task AcceptBooking_WhenPending_TransitionsToConfirmed()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m }
            }
        });

        var bookingId = createResult.Response!.Id;

        var acceptResult = await service.AcceptBookingAsync(bookingId, providerId);

        Assert.True(acceptResult.Succeeded);
        Assert.Equal("Confirmed", acceptResult.Response!.Status);
        Assert.Equal(2, acceptResult.Response.StatusHistory!.Count);
        Assert.Equal("Confirmed", acceptResult.Response.StatusHistory[1].NewStatus);
    }

    [Fact]
    public async Task AcceptBooking_WhenAlreadyConfirmed_PreventsInvalidStatusTransition()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m }
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
    public async Task RejectBooking_WhenPending_TransitionsToRejectedWithReason()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 50m }
            }
        });

        var bookingId = createResult.Response!.Id;
        const string reason = "Attraction is fully booked for this time slot.";

        var rejectResult = await service.RejectBookingAsync(bookingId, providerId, new RejectBookingRequest { Reason = reason });

        Assert.True(rejectResult.Succeeded);
        Assert.Equal("Rejected", rejectResult.Response!.Status);
        Assert.Equal(reason, rejectResult.Response.StatusHistory![1].Reason);
    }

    [Fact]
    public async Task CancelBooking_ByOwnerTourist_SucceedsAndCreatesCancellationRecord()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);
        var touristId = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristId, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 75m }
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
        Assert.Equal("Cancelled", cancelResult.Response!.Status);
        Assert.NotNull(cancelResult.Response.Cancellation);
        Assert.Equal(reason, cancelResult.Response.Cancellation!.Reason);
        Assert.Equal(touristId, cancelResult.Response.Cancellation.CancelledBy);
    }

    [Fact]
    public async Task CancelBooking_ByAnotherTourist_FailsWithOwnershipError()
    {
        await using var dbContext = CreateDbContext();
        var service = new BookingService(dbContext);
        var touristA = Guid.NewGuid();
        var touristB = Guid.NewGuid();

        var createResult = await service.CreateBookingAsync(touristA, new CreateBookingRequest
        {
            Items = new List<BookingItemRequest>
            {
                new() { AttractionId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m }
            }
        });

        var bookingId = createResult.Response!.Id;

        // Tourist B tries to cancel Tourist A's booking
        var cancelResult = await service.CancelBookingAsync(
            bookingId,
            touristB,
            nameof(UserRole.Tourist),
            new CancelBookingRequest { Reason = "Malicious cancellation attempt" });

        Assert.False(cancelResult.Succeeded);
        Assert.Equal("You cannot cancel another user's booking.", cancelResult.Error);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
