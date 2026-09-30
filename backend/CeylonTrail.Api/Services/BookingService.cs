using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.DTOs.Pagination;
using CeylonTrail.Api.DTOs.TravelAlerts;
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

        if (request.Items.Count > 100 || request.Items.Select(item => item.AvailabilitySlotId).Distinct().Count() != request.Items.Count)
        {
            return (false, "A booking must contain no more than 100 unique availability slots.", null);
        }

        // A supplied trip must exist and belong to the authenticated tourist.
        // Return the same safe not-found result for both cases so ownership is not disclosed.
        Guid? tripId = null;
        if (request.TripId.HasValue && request.TripId.Value != Guid.Empty)
        {
            var ownedTrip = await dbContext.Trips
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    trip => trip.Id == request.TripId.Value && trip.TouristId == touristId,
                    cancellationToken);
            if (ownedTrip is null)
            {
                return (false, "Trip not found.", null);
            }

            tripId = ownedTrip.Id;
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
            if (item.NumberOfGuests <= 0)
            {
                return (false, "NumberOfGuests must be greater than zero.", null);
            }

            if (item.UnitPrice.HasValue && item.UnitPrice.Value < 0)
            {
                return (false, "UnitPrice must be non-negative when supplied.", null);
            }

            var slot = await dbContext.AvailabilitySlots
                .Include(availabilitySlot => availabilitySlot.Attraction)
                .FirstOrDefaultAsync(s => s.Id == item.AvailabilitySlotId, cancellationToken);

            if (slot == null)
            {
                return (false, $"Availability slot '{item.AvailabilitySlotId}' does not exist. Please select a valid slot.", null);
            }

            if (slot.Attraction is null ||
                !slot.Attraction.IsActive ||
                !string.Equals(slot.Attraction.Status, "Approved", StringComparison.Ordinal))
            {
                return (false, "The attraction is not available for booking.", null);
            }

            if (slot.EndTime <= DateTime.UtcNow)
            {
                return (false, "The availability slot is no longer bookable.", null);
            }

            if (slot.BookedCapacity < 0 ||
                slot.MaxCapacity <= 0 ||
                slot.BookedCapacity > slot.MaxCapacity ||
                item.NumberOfGuests > slot.MaxCapacity - slot.BookedCapacity)
            {
                return (false, $"Availability slot has insufficient capacity. Available: {slot.AvailableCapacity}, Requested: {item.NumberOfGuests}.", null);
            }

            var unitPrice = slot.PricePerPerson;
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

        var advisories = await GetActiveAdvisoriesForBookingAsync(booking, cancellationToken);
        return (true, null, MapToResponse(booking, advisories));
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

        var advisoryMap = await GetActiveAdvisoriesForBookingsAsync(bookings, cancellationToken);
        return bookings.Select(b => MapToResponse(b, advisoryMap.GetValueOrDefault(b.Id))).ToList();
    }

    public async Task<List<BookingResponse>> GetProviderBookingsAsync(
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default)
    {
        var bookings = await ApplyProviderScope(
                dbContext.Bookings.AsNoTracking(),
                requestingUserId,
                requestingRole)
            .AsNoTracking()
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.CancellationRequests)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var advisoryMap = await GetActiveAdvisoriesForBookingsAsync(bookings, cancellationToken);
        return bookings.Select(b => MapToResponse(b, advisoryMap.GetValueOrDefault(b.Id))).ToList();
    }

    public async Task<PagedResponse<BookingResponse>> GetProviderBookingsPageAsync(
        Guid requestingUserId,
        string requestingRole,
        BookingQuery request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyProviderScope(
            dbContext.Bookings.AsNoTracking(),
            requestingUserId,
            requestingRole);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            if (Guid.TryParse(search, out var bookingId))
            {
                query = query.Where(booking => booking.Id == bookingId ||
                    booking.User!.FirstName.Contains(search) ||
                    booking.User.LastName.Contains(search) ||
                    booking.User.Email.Contains(search) ||
                    booking.Items.Any(item => item.AvailabilitySlot!.Attraction!.Name.Contains(search)));
            }
            else
            {
                query = query.Where(booking =>
                    booking.User!.FirstName.Contains(search) ||
                    booking.User.LastName.Contains(search) ||
                    booking.User.Email.Contains(search) ||
                    booking.Items.Any(item => item.AvailabilitySlot!.Attraction!.Name.Contains(search)));
            }
        }

        if (request.Status.HasValue)
        {
            query = query.Where(booking => booking.CurrentStatus == request.Status.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(booking => booking.CreatedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(booking => booking.CreatedAt <= request.DateTo.Value);
        }

        if (request.AttractionId.HasValue)
        {
            query = query.Where(booking => booking.Items.Any(item =>
                item.AvailabilitySlot!.AttractionId == request.AttractionId.Value));
        }

        if (request.TripId.HasValue)
        {
            query = query.Where(booking => booking.TripId == request.TripId.Value);
        }

        query = ApplyBookingOrdering(query, request.SortBy, request.SortDirection);
        var totalCount = await query.CountAsync(cancellationToken);
        var bookings = await query
            .Include(booking => booking.Items)
            .Include(booking => booking.StatusHistory)
            .Include(booking => booking.CancellationRequests)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BookingResponse>(
            bookings.Select(b => MapToResponse(b, null)).ToList(),
            totalCount,
            request.Page,
            request.PageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize));
    }

    private static IQueryable<Booking> ApplyProviderScope(
        IQueryable<Booking> query,
        Guid requestingUserId,
        string requestingRole)
    {
        if (requestingRole == nameof(UserRole.TourismProvider))
        {
            return query.Where(booking => booking.Items.Any(item =>
                item.AvailabilitySlot != null &&
                item.AvailabilitySlot.Attraction != null &&
                item.AvailabilitySlot.Attraction.ProviderId == requestingUserId));
        }

        return requestingRole == nameof(UserRole.TravelCoordinator) ||
               requestingRole == nameof(UserRole.Administrator)
            ? query
            : query.Where(_ => false);
    }

    private static IQueryable<Booking> ApplyBookingOrdering(
        IQueryable<Booking> query,
        string sortBy,
        string sortDirection)
    {
        var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("updatedat", true) => query.OrderBy(booking => booking.UpdatedAt).ThenBy(booking => booking.Id),
            ("updatedat", false) => query.OrderByDescending(booking => booking.UpdatedAt).ThenByDescending(booking => booking.Id),
            ("status", true) => query.OrderBy(booking => booking.CurrentStatus).ThenBy(booking => booking.Id),
            ("status", false) => query.OrderByDescending(booking => booking.CurrentStatus).ThenByDescending(booking => booking.Id),
            ("totalamount", true) => query.OrderBy(booking => booking.TotalAmount).ThenBy(booking => booking.Id),
            ("totalamount", false) => query.OrderByDescending(booking => booking.TotalAmount).ThenByDescending(booking => booking.Id),
            ("createdat", true) => query.OrderBy(booking => booking.CreatedAt).ThenBy(booking => booking.Id),
            _ => query.OrderByDescending(booking => booking.CreatedAt).ThenByDescending(booking => booking.Id),
        };
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

        if (!await CanAccessBookingAsync(booking, requestingUserId, requestingRole, cancellationToken))
        {
            return (false, "Booking not found.", null);
        }

        var advisories = await GetActiveAdvisoriesForBookingAsync(booking, cancellationToken);
        return (true, null, MapToResponse(booking, advisories));
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> AcceptBookingAsync(
        Guid bookingId,
        Guid changedBy,
        string? requestingRole = null,
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

        if (requestingRole is not null &&
            (requestingRole == nameof(UserRole.Tourist) ||
             !await CanManageBookingAsync(booking, changedBy, requestingRole, cancellationToken)))
        {
            return (false, "Booking not found.", null);
        }

        // Controlled workflow check
        if (booking.CurrentStatus != BookingStatus.PendingAI && booking.CurrentStatus != BookingStatus.PendingHumanApproval && booking.CurrentStatus != BookingStatus.Draft)
        {
            return (false, $"Cannot accept booking in '{booking.CurrentStatus}' status.", null);
        }

        // Deduct slot capacity now that provider has confirmed the booking
        foreach (var item in booking.Items)
        {
            var slot = await dbContext.AvailabilitySlots
                .FirstOrDefaultAsync(s => s.Id == item.AvailabilitySlotId, cancellationToken);
            if (slot != null)
            {
                if (item.NumberOfGuests > slot.MaxCapacity - slot.BookedCapacity)
                {
                    return (false, $"Cannot confirm booking: slot has insufficient capacity. Available: {slot.AvailableCapacity}, Requested: {item.NumberOfGuests}.", null);
                }

                slot.BookedCapacity += item.NumberOfGuests;
                slot.RowVersion = Guid.NewGuid().ToByteArray();
                slot.UpdatedAt = DateTime.UtcNow;

                var expSlot = await dbContext.ExperienceSlots
                    .FirstOrDefaultAsync(e => e.Id == slot.Id || (e.AttractionId == slot.AttractionId && e.Date == DateOnly.FromDateTime(slot.StartTime) && e.StartTime == TimeOnly.FromDateTime(slot.StartTime)), cancellationToken);
                if (expSlot != null)
                {
                    expSlot.AvailableCapacity = Math.Max(0, expSlot.Capacity - slot.BookedCapacity);
                }
            }
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return (false, "The selected availability changed during confirmation. Please try again.", null);
        }
        var advisories = await GetActiveAdvisoriesForBookingAsync(booking, cancellationToken);
        return (true, null, MapToResponse(booking, advisories));
    }

    public async Task<(bool Succeeded, string? Error, BookingResponse? Response)> RejectBookingAsync(
        Guid bookingId,
        Guid changedBy,
        RejectBookingRequest request,
        string? requestingRole = null,
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

        if (requestingRole is not null &&
            (requestingRole == nameof(UserRole.Tourist) ||
             !await CanManageBookingAsync(booking, changedBy, requestingRole, cancellationToken)))
        {
            return (false, "Booking not found.", null);
        }

        if (booking.CurrentStatus is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            return (false, $"Cannot reject booking in '{booking.CurrentStatus}' status.", null);
        }

        var previousStatus = booking.CurrentStatus;
        booking.CurrentStatus = BookingStatus.Rejected;
        booking.UpdatedAt = DateTime.UtcNow;

        // Release slot capacity only if booking was already confirmed
        if (previousStatus == BookingStatus.Confirmed)
        {
            foreach (var item in booking.Items)
            {
                var slot = await dbContext.AvailabilitySlots
                    .FirstOrDefaultAsync(s => s.Id == item.AvailabilitySlotId, cancellationToken);
                if (slot != null)
                {
                    slot.BookedCapacity = Math.Max(0, slot.BookedCapacity - item.NumberOfGuests);
                    slot.RowVersion = Guid.NewGuid().ToByteArray();
                    slot.UpdatedAt = DateTime.UtcNow;

                    var expSlot = await dbContext.ExperienceSlots
                        .FirstOrDefaultAsync(e => e.Id == slot.Id || (e.AttractionId == slot.AttractionId && e.Date == DateOnly.FromDateTime(slot.StartTime) && e.StartTime == TimeOnly.FromDateTime(slot.StartTime)), cancellationToken);
                    if (expSlot != null)
                    {
                        expSlot.AvailableCapacity = Math.Max(0, expSlot.Capacity - slot.BookedCapacity);
                    }
                }
            }
        }

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
        var advisories = await GetActiveAdvisoriesForBookingAsync(booking, cancellationToken);
        return (true, null, MapToResponse(booking, advisories));
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

        if (requestingRole is not (nameof(UserRole.Tourist) or nameof(UserRole.Administrator)))
        {
            return (false, "Only the booking owner or an administrator can cancel a booking.", null);
        }

        // Ownership check: Tourists can only cancel their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.UserId != requestingUserId)
        {
            return (false, "You cannot cancel another user's booking.", null);
        }

        if (booking.CurrentStatus is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return (false, $"Cannot cancel booking in '{booking.CurrentStatus}' status.", null);
        }

        var previousStatus = booking.CurrentStatus;
        booking.CurrentStatus = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;

        // Release slot capacity only if booking was already confirmed
        if (previousStatus == BookingStatus.Confirmed)
        {
            foreach (var item in booking.Items)
            {
                var slot = await dbContext.AvailabilitySlots
                    .FirstOrDefaultAsync(s => s.Id == item.AvailabilitySlotId, cancellationToken);
                if (slot != null)
                {
                    slot.BookedCapacity = Math.Max(0, slot.BookedCapacity - item.NumberOfGuests);
                    slot.RowVersion = Guid.NewGuid().ToByteArray();
                    slot.UpdatedAt = DateTime.UtcNow;

                    var expSlot = await dbContext.ExperienceSlots
                        .FirstOrDefaultAsync(e => e.Id == slot.Id || (e.AttractionId == slot.AttractionId && e.Date == DateOnly.FromDateTime(slot.StartTime) && e.StartTime == TimeOnly.FromDateTime(slot.StartTime)), cancellationToken);
                    if (expSlot != null)
                    {
                        expSlot.AvailableCapacity = Math.Max(0, expSlot.Capacity - slot.BookedCapacity);
                    }
                }
            }
        }

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
        var advisories = await GetActiveAdvisoriesForBookingAsync(booking, cancellationToken);
        return (true, null, MapToResponse(booking, advisories));
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

        if (!await CanAccessBookingAsync(booking, requestingUserId, requestingRole, cancellationToken))
        {
            return (false, "Booking not found.", null);
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
        // Auto-synchronize ExperienceSlots on approved attractions to AvailabilitySlots
        var expQuery = dbContext.ExperienceSlots
            .Include(e => e.Attraction)
            .Where(e => e.Attraction != null && e.Attraction.IsActive && e.Attraction.Status == "Approved");

        if (attractionId.HasValue && attractionId.Value != Guid.Empty)
        {
            expQuery = expQuery.Where(e => e.AttractionId == attractionId.Value);
        }

        var experienceSlots = await expQuery.ToListAsync(cancellationToken);
        var addedAny = false;

        foreach (var exp in experienceSlots)
        {
            var startUtc = DateTime.SpecifyKind(exp.Date.ToDateTime(exp.StartTime), DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(exp.Date.ToDateTime(exp.EndTime), DateTimeKind.Utc);

            var existingSlot = await dbContext.AvailabilitySlots.FirstOrDefaultAsync(
                s => s.Id == exp.Id || (s.AttractionId == exp.AttractionId && s.StartTime == startUtc && s.EndTime == endUtc),
                cancellationToken);

            if (existingSlot == null)
            {
                dbContext.AvailabilitySlots.Add(new AvailabilitySlot
                {
                    Id = exp.Id,
                    AttractionId = exp.AttractionId,
                    StartTime = startUtc,
                    EndTime = endUtc,
                    MaxCapacity = exp.Capacity,
                    BookedCapacity = Math.Max(0, exp.Capacity - exp.AvailableCapacity),
                    PricePerPerson = exp.Attraction!.Price,
                    RowVersion = new byte[] { 0 },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                addedAny = true;
            }
            else
            {
                var calculatedAvailable = Math.Max(0, exp.Capacity - existingSlot.BookedCapacity);
                if (exp.AvailableCapacity != calculatedAvailable)
                {
                    exp.AvailableCapacity = calculatedAvailable;
                    addedAny = true;
                }
            }
        }

        if (addedAny)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var query = dbContext.AvailabilitySlots
            .AsNoTracking()
            .Where(slot =>
                slot.Attraction != null &&
                slot.Attraction.IsActive &&
                slot.Attraction.Status == "Approved" &&
                slot.BookedCapacity < slot.MaxCapacity &&
                slot.EndTime > DateTime.UtcNow);

        if (attractionId.HasValue && attractionId.Value != Guid.Empty)
        {
            query = query.Where(s => s.AttractionId == attractionId.Value);
        }

        var nowUtc = DateTime.UtcNow;
        var slots = await query
            .Where(s => s.EndTime > nowUtc)
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
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.AttractionId, cancellationToken);
        if (attraction is null ||
            (requestingRole == nameof(UserRole.TourismProvider) && attraction.ProviderId != requestingUserId))
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
            RowVersion = new byte[] { 0 },
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

    public async Task<(bool Succeeded, string? Error)> DeleteBookingAsync(
        Guid bookingId,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.StatusHistory)
            .Include(b => b.CancellationRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            return (false, "Booking not found.");
        }

        if (requestingRole is not (nameof(UserRole.Tourist) or nameof(UserRole.Administrator)))
        {
            return (false, "Only the booking owner or an administrator can delete a booking.");
        }

        // Ownership check: Tourists can only delete their own booking
        if (requestingRole == nameof(UserRole.Tourist) && booking.UserId != requestingUserId)
        {
            return (false, "You cannot delete another user's booking.");
        }

        if (booking.CurrentStatus != BookingStatus.Draft && requestingRole != nameof(UserRole.Administrator))
        {
            return (false, "Only draft bookings can be deleted.");
        }

        dbContext.Bookings.Remove(booking);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private async Task<bool> CanAccessBookingAsync(
        Booking booking,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken)
    {
        if (requestingRole == nameof(UserRole.Tourist))
        {
            return booking.UserId == requestingUserId;
        }

        if (requestingRole is nameof(UserRole.TravelCoordinator) or nameof(UserRole.Administrator))
        {
            return true;
        }

        if (requestingRole != nameof(UserRole.TourismProvider))
        {
            return false;
        }

        return await dbContext.BookingItems
            .Where(item => item.BookingId == booking.Id)
            .AnyAsync(item => item.AvailabilitySlot != null &&
                              item.AvailabilitySlot.Attraction != null &&
                              item.AvailabilitySlot.Attraction.ProviderId == requestingUserId,
                cancellationToken);
    }

    private async Task<bool> CanManageBookingAsync(
        Booking booking,
        Guid requestingUserId,
        string requestingRole,
        CancellationToken cancellationToken)
    {
        if (requestingRole is nameof(UserRole.TravelCoordinator) or nameof(UserRole.Administrator))
        {
            return true;
        }

        if (requestingRole != nameof(UserRole.TourismProvider))
        {
            return false;
        }

        return await dbContext.BookingItems
            .Where(item => item.BookingId == booking.Id)
            .AllAsync(item => item.AvailabilitySlot != null &&
                              item.AvailabilitySlot.Attraction != null &&
                              item.AvailabilitySlot.Attraction.ProviderId == requestingUserId,
                cancellationToken);
    }

    private static BookingResponse MapToResponse(Booking booking, List<TravelAlertResponse>? advisories = null)
    {
        return new BookingResponse(
            booking.Id,
            booking.TripId,
            booking.CurrentStatus.ToString(),
            booking.TotalAmount,
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
                cr.RequestedAt)).ToList(),
            advisories ?? new List<TravelAlertResponse>()
        );
    }

    private async Task<List<TravelAlertResponse>> GetActiveAdvisoriesForBookingAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        var result = await GetActiveAdvisoriesForBookingsAsync([booking], cancellationToken);
        return result.TryGetValue(booking.Id, out var list) ? list : new List<TravelAlertResponse>();
    }

    private async Task<Dictionary<Guid, List<TravelAlertResponse>>> GetActiveAdvisoriesForBookingsAsync(
        IEnumerable<Booking> bookings,
        CancellationToken cancellationToken = default)
    {
        var bookingList = bookings.ToList();
        var result = new Dictionary<Guid, List<TravelAlertResponse>>();
        if (bookingList.Count == 0)
        {
            return result;
        }

        var slotIds = bookingList
            .SelectMany(b => b.Items)
            .Select(i => i.AvailabilitySlotId)
            .Distinct()
            .ToList();

        if (slotIds.Count == 0)
        {
            foreach (var b in bookingList)
            {
                result[b.Id] = new List<TravelAlertResponse>();
            }
            return result;
        }

        var slots = await dbContext.AvailabilitySlots
            .AsNoTracking()
            .Include(s => s.Attraction)
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var activeAlerts = await dbContext.TravelAlerts
            .AsNoTracking()
            .Where(a => a.Status == TravelAlertStatus.Active && a.EndDateTime > now)
            .ToListAsync(cancellationToken);

        foreach (var booking in bookingList)
        {
            var matchedAlerts = new List<TravelAlert>();
            foreach (var item in booking.Items)
            {
                if (!slots.TryGetValue(item.AvailabilitySlotId, out var slot) ||
                    slot.Attraction == null ||
                    string.IsNullOrWhiteSpace(slot.Attraction.District))
                {
                    continue;
                }

                var district = slot.Attraction.District.Trim();
                var alertsForSlot = activeAlerts.Where(alert =>
                    string.Equals(alert.District.Trim(), district, StringComparison.OrdinalIgnoreCase) &&
                    alert.StartDateTime <= slot.EndTime &&
                    alert.EndDateTime >= slot.StartTime);

                matchedAlerts.AddRange(alertsForSlot);
            }

            result[booking.Id] = matchedAlerts
                .DistinctBy(a => a.Id)
                .Select(alert => new TravelAlertResponse
                {
                    Id = alert.Id,
                    Title = alert.Title,
                    Description = alert.Description,
                    AlertType = alert.AlertType,
                    Severity = alert.Severity,
                    District = alert.District,
                    StartDateTime = alert.StartDateTime,
                    EndDateTime = alert.EndDateTime,
                    Status = alert.Status,
                    Source = alert.Source,
                    CreatedByUserId = alert.CreatedByUserId,
                    CreatedAt = alert.CreatedAt,
                    UpdatedAt = alert.UpdatedAt
                })
                .ToList();
        }

        return result;
    }
}
