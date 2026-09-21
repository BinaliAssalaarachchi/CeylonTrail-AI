using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class BookingService(ApplicationDbContext dbContext) : IBookingService
{
    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> CreateBookingAsync(
        Guid touristId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return (false, "A booking must contain at least one item.", null);
        }

        var booking = new Booking
        {
            TouristId = touristId,
            TripId = request.TripId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var subtotal = item.Quantity * item.UnitPrice;
            total += subtotal;

            booking.Items.Add(new BookingItem
            {
                BookingId = booking.Id,
                AttractionId = item.AttractionId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = subtotal
            });
        }

        booking.TotalAmount = total;

        // Record initial status history
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = BookingStatus.Pending,
            NewStatus = BookingStatus.Pending,
            ChangedBy = touristId,
            ChangedAt = DateTime.UtcNow,
            Reason = "Booking request submitted."
        });

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, MapToResponse(booking));
    }

    public async Task<List<BookingResponse>> GetTouristBookingsAsync(
        Guid touristId,
        CancellationToken cancellationToken = default)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .Where(b => b.TouristId == touristId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(MapToResponse).ToList();
    }

    public async Task<List<BookingResponse>> GetProviderBookingsAsync(
        CancellationToken cancellationToken = default)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(MapToResponse).ToList();
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> GetBookingByIdAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Authorization check: Tourist can only see their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.TouristId != requestingUserId)
        {
            return (false, "Unauthorized access to this booking.", null);
        }

        return (true, null, MapToResponse(booking));
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> AcceptBookingAsync(
        Guid bookingId,
        Guid changedBy,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Controlled workflow check
        if (booking.Status != BookingStatus.Pending)
        {
            return (false, $"Cannot accept booking in '{booking.Status}' status. Only Pending bookings can be accepted.", null);
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Confirmed,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Reason = "Booking accepted by provider/staff."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, MapToResponse(booking));
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> RejectBookingAsync(
        Guid bookingId,
        Guid changedBy,
        RejectBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Controlled workflow check
        if (booking.Status != BookingStatus.Pending)
        {
            return (false, $"Cannot reject booking in '{booking.Status}' status. Only Pending bookings can be rejected.", null);
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;

        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Rejected,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Reason = request.Reason
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, MapToResponse(booking));
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> CancelBookingAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancelBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.Cancellation)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Ownership check: Tourists can only cancel their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.TouristId != requestingUserId)
        {
            return (false, "You cannot cancel another user's booking.", null);
        }

        // Controlled workflow check: only Pending or Confirmed bookings can be cancelled
        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            return (false, $"Cannot cancel booking in '{booking.Status}' status. Only Pending or Confirmed bookings can be cancelled.", null);
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        // Record cancellation details
        var cancellation = new Cancellation
        {
            BookingId = booking.Id,
            Reason = request.Reason,
            CancelledBy = requestingUserId,
            CancelledAt = DateTime.UtcNow
        };
        booking.Cancellation = cancellation;
        dbContext.Cancellations.Add(cancellation);

        // Record status history
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Cancelled,
            ChangedBy = requestingUserId,
            ChangedAt = DateTime.UtcNow,
            Reason = request.Reason
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, MapToResponse(booking));
    }

    public async Task<(bool Succeeded, string? Error, List<BookingHistoryResponse>? Response)> GetBookingHistoryAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.StatusHistory)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        if (requestingRole == nameof(UserRole.Tourist) && booking.TouristId != requestingUserId)
        {
            return (false, "Unauthorized access to this booking history.", null);
        }

        var history = booking.StatusHistory
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new BookingHistoryResponse(
                h.Id,
                h.PreviousStatus.ToString(),
                h.NewStatus.ToString(),
                h.ChangedBy,
                h.ChangedAt,
                h.Reason))
            .ToList();

        return (true, null, history);
    }

    private static BookingResponse MapToResponse(Booking booking)
    {
        return new BookingResponse(
            booking.Id,
            booking.TouristId,
            booking.TripId,
            booking.Status.ToString(),
            booking.TotalAmount,
            booking.CreatedAt,
            booking.UpdatedAt,
            booking.Items.Select(i => new BookingItemResponse(
                i.Id,
                i.AttractionId,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal)).ToList(),
            booking.StatusHistory.Select(h => new BookingHistoryResponse(
                h.Id,
                h.PreviousStatus.ToString(),
                h.NewStatus.ToString(),
                h.ChangedBy,
                h.ChangedAt,
                h.Reason)).ToList(),
            booking.Cancellation != null
                ? new CancellationResponse(
                    booking.Cancellation.Id,
                    booking.Cancellation.Reason,
                    booking.Cancellation.CancelledBy,
                    booking.Cancellation.CancelledAt)
                : null
        );
    }
}
