using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.TravelAlerts;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class TravelAlertService(ApplicationDbContext dbContext) : ITravelAlertService
{
    public async Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> CreateAsync(
        CreateTravelAlertRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(
            request.Title,
            request.Description,
            request.District,
            request.StartDateTime,
            request.EndDateTime,
            out var startDateTime,
            out var endDateTime);

        if (validationError is not null)
        {
            return (false, validationError, null);
        }

        var creator = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == authenticatedUserId && user.IsActive,
            cancellationToken);

        if (creator is null)
        {
            return (false, "The authenticated creator does not exist or is inactive.", null);
        }

        var now = DateTime.UtcNow;
        var alert = new TravelAlert
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AlertType = request.AlertType,
            Severity = request.Severity,
            District = request.District.Trim(),
            StartDateTime = startDateTime,
            EndDateTime = endDateTime,
            Status = request.Status,
            Source = request.Source?.Trim(),
            CreatedByUserId = creator.Id,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUser = creator
        };

        dbContext.TravelAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, ToResponse(alert));
    }

    public async Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.TravelAlerts
            .Include(candidate => candidate.CreatedByUser)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return alert is null
            ? (false, "Travel alert was not found.", null)
            : (true, null, ToResponse(alert));
    }

    public async Task<(bool Succeeded, string? Error, TravelAlertPageResponse? Response)> QueryAsync(
        TravelAlertQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Page < 1)
        {
            return (false, "Page must be at least 1.", null);
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            return (false, "PageSize must be between 1 and 100.", null);
        }

        var sortBy = (request.SortBy ?? string.Empty).Trim().ToLowerInvariant();
        var sortDirection = (request.SortDirection ?? string.Empty).Trim().ToLowerInvariant();
        var supportedSortFields = new[]
        {
            "createdat",
            "updatedat",
            "startdatetime",
            "enddatetime",
            "severity",
            "district",
            "title"
        };

        if (!supportedSortFields.Contains(sortBy))
        {
            return (false, "SortBy is not supported.", null);
        }

        if (sortDirection is not ("asc" or "desc"))
        {
            return (false, "SortDirection must be either 'asc' or 'desc'.", null);
        }

        IQueryable<TravelAlert> query = dbContext.TravelAlerts
            .AsNoTracking()
            .Include(alert => alert.CreatedByUser);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(alert =>
                alert.Title.ToLower().Contains(search) ||
                alert.Description.ToLower().Contains(search) ||
                alert.District.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            var district = request.District.Trim();
            query = query.Where(alert => alert.District == district);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(alert => alert.Status == request.Status.Value);
        }

        if (request.Severity.HasValue)
        {
            query = query.Where(alert => alert.Severity == request.Severity.Value);
        }

        if (request.AlertType.HasValue)
        {
            query = query.Where(alert => alert.AlertType == request.AlertType.Value);
        }

        query = ApplySorting(query, sortBy, sortDirection == "asc");

        var totalCount = await query.CountAsync(cancellationToken);
        var alerts = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return (true, null, new TravelAlertPageResponse
        {
            Items = alerts.Select(ToResponse).ToArray(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    public async Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> UpdateAsync(
        Guid id,
        UpdateTravelAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(
            request.Title,
            request.Description,
            request.District,
            request.StartDateTime,
            request.EndDateTime,
            out var startDateTime,
            out var endDateTime);

        if (validationError is not null)
        {
            return (false, validationError, null);
        }

        var alert = await dbContext.TravelAlerts
            .Include(candidate => candidate.CreatedByUser)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (alert is null)
        {
            return (false, "Travel alert was not found.", null);
        }

        alert.Title = request.Title.Trim();
        alert.Description = request.Description.Trim();
        alert.AlertType = request.AlertType;
        alert.Severity = request.Severity;
        alert.District = request.District.Trim();
        alert.StartDateTime = startDateTime;
        alert.EndDateTime = endDateTime;
        alert.Status = request.Status;
        alert.Source = request.Source?.Trim();
        alert.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, ToResponse(alert));
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.TravelAlerts
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (alert is null)
        {
            return (false, "Travel alert was not found.");
        }

        dbContext.TravelAlerts.Remove(alert);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    private static string? ValidateRequest(
        string title,
        string description,
        string district,
        DateTime requestedStartDateTime,
        DateTime requestedEndDateTime,
        out DateTime startDateTime,
        out DateTime endDateTime)
    {
        startDateTime = ToUtc(requestedStartDateTime);
        endDateTime = ToUtc(requestedEndDateTime);

        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title cannot be blank.";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return "Description cannot be blank.";
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            return "District cannot be blank.";
        }

        if (endDateTime <= startDateTime)
        {
            return "EndDateTime must be later than StartDateTime.";
        }

        return null;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static IQueryable<TravelAlert> ApplySorting(
        IQueryable<TravelAlert> query,
        string sortBy,
        bool ascending)
    {
        if (ascending)
        {
            return sortBy switch
            {
                "createdat" => query.OrderBy(alert => alert.CreatedAt),
                "updatedat" => query.OrderBy(alert => alert.UpdatedAt),
                "startdatetime" => query.OrderBy(alert => alert.StartDateTime),
                "enddatetime" => query.OrderBy(alert => alert.EndDateTime),
                "severity" => query.OrderBy(alert => alert.Severity),
                "district" => query.OrderBy(alert => alert.District),
                "title" => query.OrderBy(alert => alert.Title),
                _ => query
            };
        }

        return sortBy switch
        {
            "createdat" => query.OrderByDescending(alert => alert.CreatedAt),
            "updatedat" => query.OrderByDescending(alert => alert.UpdatedAt),
            "startdatetime" => query.OrderByDescending(alert => alert.StartDateTime),
            "enddatetime" => query.OrderByDescending(alert => alert.EndDateTime),
            "severity" => query.OrderByDescending(alert => alert.Severity),
            "district" => query.OrderByDescending(alert => alert.District),
            "title" => query.OrderByDescending(alert => alert.Title),
            _ => query
        };
    }

    private static TravelAlertResponse ToResponse(TravelAlert alert) => new()
    {
        Id = alert.Id,
        Title = alert.Title,
        Description = alert.Description,
        AlertType = alert.AlertType,
        Severity = alert.Severity,
        District = alert.District,
        StartDateTime = ToUtc(alert.StartDateTime),
        EndDateTime = ToUtc(alert.EndDateTime),
        Status = alert.Status,
        Source = alert.Source,
        CreatedByUserId = alert.CreatedByUserId,
        CreatedAt = ToUtc(alert.CreatedAt),
        UpdatedAt = ToUtc(alert.UpdatedAt),
        CreatedByUserName = alert.CreatedByUser is null
            ? null
            : $"{alert.CreatedByUser.FirstName} {alert.CreatedByUser.LastName}".Trim()
    };
}
