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

        // Validate Trip if provided
        Guid? tripId = null;
        if (request.TripId.HasValue && request.TripId.Value != Guid.Empty)
        {
            var tripExists = await dbContext.Trips.AnyAsync(t => t.Id == request.TripId.Value, cancellationToken);
            if (tripExists)
            {
                tripId = request.TripId.Value;
            }
        }

        var booking = new Booking
        {
            UserId = touristId,
            TripId = tripId,
            CurrentStatus = BookingStatus.Draft,
            QrCodeHash = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var slot = await dbContext.AvailabilitySlots
                .FirstOrDefaultAsync(s => s.Id == item.AvailabilitySlotId, cancellationToken);

            if (slot == null)
            {
                return (false, $"Availability slot '{item.AvailabilitySlotId}' does not exist. Please select a valid slot.", null);
            }

            if (slot.BookedCapacity + item.NumberOfGuests > slot.MaxCapacity)
            {
                return (false, $"Availability slot has insufficient capacity. Available: {slot.AvailableCapacity}, Requested: {item.NumberOfGuests}.", null);
            }

            // Reserve slot capacity
            slot.BookedCapacity += item.NumberOfGuests;
            slot.UpdatedAt = DateTime.UtcNow;

            var unitPrice = item.UnitPrice > 0 ? item.UnitPrice : slot.PricePerPerson;
            var subtotal = item.NumberOfGuests * unitPrice;
            total += subtotal;

            booking.Items.Add(new BookingItem
            {
                BookingId = booking.Id,
                AvailabilitySlotId = item.AvailabilitySlotId,
                NumberOfGuests = item.NumberOfGuests,
                UnitPrice = unitPrice,
                SubTotal = subtotal
            });
        }

        booking.TotalAmount = total;

        // Record initial status history
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = BookingStatus.Draft,
            NewStatus = BookingStatus.Draft,
            ChangedByUserId = touristId,
            Timestamp = DateTime.UtcNow,
            Reason = "Booking draft created."
        });

        try
        {
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "The selected slot was modified by another booking. Please try again.", null);
        }

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
            .Include(b => b.CancellationRequests)
            .Where(b => b.UserId == touristId)
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
            .Include(b => b.CancellationRequests)
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
            .Include(b => b.CancellationRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Authorization check: Tourist can only see their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.UserId != requestingUserId)
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
            .Include(b => b.CancellationRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Controlled workflow check
        if (booking.CurrentStatus != BookingStatus.PendingAI && booking.CurrentStatus != BookingStatus.PendingHumanApproval && booking.CurrentStatus != BookingStatus.Draft)
        {
            return (false, $"Cannot accept booking in '{booking.CurrentStatus}' status.", null);
        }

        var previousStatus = booking.CurrentStatus;
        booking.CurrentStatus = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Confirmed,
            ChangedByUserId = changedBy,
            Timestamp = DateTime.UtcNow,
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
            .Include(b => b.CancellationRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        var previousStatus = booking.CurrentStatus;
        booking.CurrentStatus = BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;

        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Rejected,
            ChangedByUserId = changedBy,
            Timestamp = DateTime.UtcNow,
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
            .Include(b => b.CancellationRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.", null);
        }

        // Ownership check: Tourists can only cancel their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.UserId != requestingUserId)
        {
            return (false, "You cannot cancel another user's booking.", null);
        }

        var previousStatus = booking.CurrentStatus;
        booking.CurrentStatus = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        // Record cancellation request
        var cancellationRequest = new CancellationRequest
        {
            BookingId = booking.Id,
            Reason = request.Reason,
            Status = CancellationRequestStatus.Approved,
            RefundAmount = booking.TotalAmount,
            RequestedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow,
            ReviewedByUserId = requestingUserId
        };
        booking.CancellationRequests.Add(cancellationRequest);

        // Record status history
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = previousStatus,
            NewStatus = BookingStatus.Cancelled,
            ChangedByUserId = requestingUserId,
            Timestamp = DateTime.UtcNow,
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

        if (requestingRole == nameof(UserRole.Tourist) && booking.UserId != requestingUserId)
        {
            return (false, "Unauthorized access to this booking history.", null);
        }

        var history = booking.StatusHistory
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new BookingHistoryResponse(
                h.Id,
                h.PreviousStatus.ToString(),
                h.NewStatus.ToString(),
                h.ChangedByUserId,
                h.Timestamp,
                h.Reason))
            .ToList();

        return (true, null, history);
    }

    public async Task<List<AvailabilitySlotResponse>> GetAvailabilitySlotsAsync(
        Guid? attractionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AvailabilitySlots.AsNoTracking();

        if (attractionId.HasValue && attractionId.Value != Guid.Empty)
        {
            query = query.Where(s => s.AttractionId == attractionId.Value);
        }

        var slots = await query
            .OrderBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return slots.Select(s => new AvailabilitySlotResponse(
            s.Id,
            s.AttractionId,
            s.StartTime,
            s.EndTime,
            s.MaxCapacity,
            s.BookedCapacity,
            s.AvailableCapacity,
            s.PricePerPerson
        )).ToList();
    }

    public async Task<(bool Succeeded, string? Error, AvailabilitySlotResponse? Response)> CreateAvailabilitySlotAsync(
        CreateAvailabilitySlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var attractionExists = await dbContext.Attractions.AnyAsync(a => a.Id == request.AttractionId, cancellationToken);
        if (!attractionExists)
        {
            return (false, "Attraction not found.", null);
        }

        if (request.StartTime >= request.EndTime)
        {
            return (false, "StartTime must be earlier than EndTime.", null);
        }

        var slot = new AvailabilitySlot
        {
            AttractionId = request.AttractionId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            MaxCapacity = request.MaxCapacity,
            BookedCapacity = 0,
            PricePerPerson = request.PricePerPerson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, new AvailabilitySlotResponse(
            slot.Id,
            slot.AttractionId,
            slot.StartTime,
            slot.EndTime,
            slot.MaxCapacity,
            slot.BookedCapacity,
            slot.AvailableCapacity,
            slot.PricePerPerson
        ));
    }

    private static BookingResponse MapToResponse(Booking booking)
    {
        return new BookingResponse(
            booking.Id,
            booking.UserId,
            booking.TripId,
            booking.CurrentStatus.ToString(),
            booking.TotalAmount,
            booking.QrCodeHash,
            booking.CreatedAt,
            booking.UpdatedAt,
            booking.Items.Select(i => new BookingItemResponse(
                i.Id,
                i.AvailabilitySlotId,
                i.NumberOfGuests,
                i.UnitPrice,
                i.SubTotal)).ToList(),
            booking.StatusHistory.Select(h => new BookingHistoryResponse(
                h.Id,
                h.PreviousStatus.ToString(),
                h.NewStatus.ToString(),
                h.ChangedByUserId,
                h.Timestamp,
                h.Reason)).ToList(),
            booking.CancellationRequests.Select(cr => new CancellationRequestResponse(
                cr.Id,
                cr.Reason,
                cr.Status.ToString(),
                cr.RefundAmount,
                cr.RequestedAt)).ToList()
        );
    }
}
