using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class AttractionService(ApplicationDbContext dbContext) : IAttractionService
{
    private const string PendingApprovalStatus = "PendingApproval";
    private const string ApprovedStatus = "Approved";

    public async Task<ServiceResult<AttractionResponse>> CreateAsync(
        CreateAttractionRequest request,
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var providerResult = await RequireProviderAsync(providerId, cancellationToken);
        if (!providerResult.Succeeded)
        {
            return ServiceResult<AttractionResponse>.Failure(providerResult.Error!, providerResult.ErrorCode);
        }

        if (!await dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
        {
            return ServiceResult<AttractionResponse>.Failure("The selected category does not exist.", ServiceErrorCode.Validation);
        }

        var now = DateTime.UtcNow;
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            District = request.District.Trim(),
            Address = request.Address.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Price = request.Price,
            Status = PendingApprovalStatus,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Attractions.Add(attraction);
        await dbContext.SaveChangesAsync(cancellationToken);

        var saved = await GetAttractionQuery()
            .SingleAsync(candidate => candidate.Id == attraction.Id, cancellationToken);
        return ServiceResult<AttractionResponse>.Success(ToResponse(saved, false));
    }

    public async Task<ServiceResult<AttractionSearchResponse>> SearchAsync(
        AttractionSearchRequest request,
        Guid? viewerId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice > request.MaxPrice)
        {
            return ServiceResult<AttractionSearchResponse>.Failure(
                "MinPrice must be less than or equal to MaxPrice.",
                ServiceErrorCode.Validation);
        }

        var query = GetAttractionQuery()
            .Where(attraction => attraction.IsActive && attraction.Status == ApprovedStatus);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(attraction =>
                EF.Functions.ILike(attraction.Name, $"%{keyword}%") ||
                EF.Functions.ILike(attraction.Description, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            var district = request.District.Trim();
            query = query.Where(attraction => EF.Functions.ILike(attraction.District, district));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(attraction => attraction.CategoryId == request.CategoryId.Value);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(attraction => attraction.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(attraction => attraction.Price <= request.MaxPrice.Value);
        }

        if (request.Date.HasValue)
        {
            query = query.Where(attraction => attraction.ExperienceSlots.Any(slot =>
                slot.Date == request.Date.Value && slot.AvailableCapacity > 0));
        }

        query = request.Sort.Trim().ToLowerInvariant() switch
        {
            "name_desc" => query.OrderByDescending(attraction => attraction.Name),
            "price_asc" => query.OrderBy(attraction => attraction.Price),
            "price_desc" => query.OrderByDescending(attraction => attraction.Price),
            "newest" => query.OrderByDescending(attraction => attraction.CreatedAt),
            "name_asc" or "" => query.OrderBy(attraction => attraction.Name),
            _ => query.OrderBy(attraction => attraction.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var favoriteIds = viewerId.HasValue
            ? (await dbContext.Favorites
                .Where(favorite => favorite.TouristId == viewerId.Value && items.Select(item => item.Id).Contains(favorite.AttractionId))
                .Select(favorite => favorite.AttractionId)
                .ToListAsync(cancellationToken)).ToHashSet()
            : new HashSet<Guid>();

        var response = new AttractionSearchResponse(
            items.Select(item => ToResponse(item, favoriteIds.Contains(item.Id))).ToList(),
            totalCount,
            request.Page,
            request.PageSize,
            (int)Math.Ceiling(totalCount / (double)request.PageSize));

        return ServiceResult<AttractionSearchResponse>.Success(response);
    }

    public async Task<ServiceResult<AttractionResponse>> GetByIdAsync(
        Guid attractionId,
        Guid? viewerId = null,
        CancellationToken cancellationToken = default)
    {
        var attraction = await GetAttractionQuery()
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == attractionId &&
                candidate.IsActive &&
                candidate.Status == ApprovedStatus,
                cancellationToken);

        if (attraction is null)
        {
            return ServiceResult<AttractionResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var isFavorite = viewerId.HasValue && await dbContext.Favorites.AnyAsync(
            favorite => favorite.TouristId == viewerId.Value && favorite.AttractionId == attractionId,
            cancellationToken);

        return ServiceResult<AttractionResponse>.Success(ToResponse(attraction, isFavorite));
    }

    public async Task<ServiceResult<AttractionResponse>> UpdateAsync(
        Guid attractionId,
        UpdateAttractionRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions
            .SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<AttractionResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<AttractionResponse>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        if (!await dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
        {
            return ServiceResult<AttractionResponse>.Failure("The selected category does not exist.", ServiceErrorCode.Validation);
        }

        attraction.CategoryId = request.CategoryId;
        attraction.Name = request.Name.Trim();
        attraction.Description = request.Description.Trim();
        attraction.District = request.District.Trim();
        attraction.Address = request.Address.Trim();
        attraction.Latitude = request.Latitude;
        attraction.Longitude = request.Longitude;
        attraction.Price = request.Price;
        attraction.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        var saved = await GetAttractionQuery().SingleAsync(candidate => candidate.Id == attractionId, cancellationToken);
        return ServiceResult<AttractionResponse>.Success(ToResponse(saved, false));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        Guid attractionId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions
            .SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<bool>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<bool>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        attraction.IsActive = false;
        attraction.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<AttractionScheduleResponse>> AddScheduleAsync(
        Guid attractionId,
        CreateScheduleRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions.SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<AttractionScheduleResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<AttractionScheduleResponse>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        if (!request.IsClosed &&
            (!request.OpeningTime.HasValue ||
             !request.ClosingTime.HasValue ||
             request.OpeningTime >= request.ClosingTime))
        {
            return ServiceResult<AttractionScheduleResponse>.Failure(
                "An open schedule requires opening and closing times, with opening time before closing time.",
                ServiceErrorCode.Validation);
        }

        if (await dbContext.AttractionSchedules.AnyAsync(schedule =>
                schedule.AttractionId == attractionId && schedule.DayOfWeek == request.DayOfWeek,
                cancellationToken))
        {
            return ServiceResult<AttractionScheduleResponse>.Failure("A schedule already exists for that day.", ServiceErrorCode.Conflict);
        }

        var schedule = new AttractionSchedule
        {
            Id = Guid.NewGuid(),
            AttractionId = attractionId,
            DayOfWeek = request.DayOfWeek,
            OpeningTime = request.OpeningTime,
            ClosingTime = request.ClosingTime,
            IsClosed = request.IsClosed
        };

        dbContext.AttractionSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<AttractionScheduleResponse>.Success(ToResponse(schedule));
    }

    public async Task<ServiceResult<ExperienceSlotResponse>> AddSlotAsync(
        Guid attractionId,
        CreateExperienceSlotRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions.SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<ExperienceSlotResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<ExperienceSlotResponse>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        if (request.StartTime >= request.EndTime)
        {
            return ServiceResult<ExperienceSlotResponse>.Failure(
                "StartTime must be before EndTime.",
                ServiceErrorCode.Validation);
        }

        if (request.Capacity <= 0 || request.AvailableCapacity < 0 || request.AvailableCapacity > request.Capacity)
        {
            return ServiceResult<ExperienceSlotResponse>.Failure(
                "Capacity must be positive and AvailableCapacity must be between zero and Capacity.",
                ServiceErrorCode.Validation);
        }

        if (await dbContext.ExperienceSlots.AnyAsync(slot =>
                slot.AttractionId == attractionId && slot.Date == request.Date && slot.StartTime == request.StartTime,
                cancellationToken))
        {
            return ServiceResult<ExperienceSlotResponse>.Failure("A slot already exists for that date and start time.", ServiceErrorCode.Conflict);
        }

        var slot = new ExperienceSlot
        {
            Id = Guid.NewGuid(),
            AttractionId = attractionId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Capacity = request.Capacity,
            AvailableCapacity = request.AvailableCapacity
        };

        dbContext.ExperienceSlots.Add(slot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ExperienceSlotResponse>.Success(ToResponse(slot));
    }

    public async Task<ServiceResult<AvailabilityResponse>> GetAvailabilityAsync(
        Guid attractionId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Attractions.AnyAsync(attraction =>
            attraction.Id == attractionId && attraction.IsActive && attraction.Status == ApprovedStatus,
            cancellationToken);
        if (!exists)
        {
            return ServiceResult<AvailabilityResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var query = dbContext.ExperienceSlots
            .AsNoTracking()
            .Where(slot => slot.AttractionId == attractionId && slot.AvailableCapacity > 0);

        if (date.HasValue)
        {
            query = query.Where(slot => slot.Date == date.Value);
        }

        var slots = await query
            .OrderBy(slot => slot.Date)
            .ThenBy(slot => slot.StartTime)
            .ToListAsync(cancellationToken);

        return ServiceResult<AvailabilityResponse>.Success(new AvailabilityResponse(
            attractionId,
            date,
            slots.Select(ToResponse).ToList()));
    }

    public async Task<ServiceResult<FavoriteResponse>> AddFavoriteAsync(
        Guid attractionId,
        Guid touristId,
        CancellationToken cancellationToken = default)
    {
        var touristResult = await RequireTouristAsync(touristId, cancellationToken);
        if (!touristResult.Succeeded)
        {
            return ServiceResult<FavoriteResponse>.Failure(touristResult.Error!, touristResult.ErrorCode);
        }

        if (!await dbContext.Attractions.AnyAsync(attraction =>
                attraction.Id == attractionId && attraction.IsActive && attraction.Status == ApprovedStatus,
                cancellationToken))
        {
            return ServiceResult<FavoriteResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        if (await dbContext.Favorites.AnyAsync(favorite =>
                favorite.TouristId == touristId && favorite.AttractionId == attractionId,
                cancellationToken))
        {
            return ServiceResult<FavoriteResponse>.Failure("The attraction is already favorited.", ServiceErrorCode.Conflict);
        }

        var favorite = new Favorite
        {
            Id = Guid.NewGuid(),
            TouristId = touristId,
            AttractionId = attractionId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Favorites.Add(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<FavoriteResponse>.Success(new FavoriteResponse(favorite.TouristId, favorite.AttractionId, favorite.CreatedAt));
    }

    public async Task<ServiceResult<bool>> RemoveFavoriteAsync(
        Guid attractionId,
        Guid touristId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await dbContext.Favorites.SingleOrDefaultAsync(candidate =>
            candidate.TouristId == touristId && candidate.AttractionId == attractionId,
            cancellationToken);
        if (favorite is null)
        {
            return ServiceResult<bool>.Failure("Favorite not found.", ServiceErrorCode.NotFound);
        }

        dbContext.Favorites.Remove(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<AttractionImageResponse>> AddImageAsync(
        Guid attractionId,
        AttractionImageRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions.SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<AttractionImageResponse>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<AttractionImageResponse>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        var image = new AttractionImage
        {
            Id = Guid.NewGuid(),
            AttractionId = attractionId,
            ImageUrl = request.ImageUrl.Trim(),
            AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.AttractionImages.Add(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<AttractionImageResponse>.Success(ToResponse(image));
    }

    public async Task<ServiceResult<bool>> RemoveImageAsync(
        Guid attractionId,
        Guid imageId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var attraction = await dbContext.Attractions.SingleOrDefaultAsync(candidate => candidate.Id == attractionId, cancellationToken);
        if (attraction is null)
        {
            return ServiceResult<bool>.Failure("Attraction not found.", ServiceErrorCode.NotFound);
        }

        var authorization = await RequireOwnerOrAdministratorAsync(attraction, actorId, cancellationToken);
        if (!authorization.Succeeded)
        {
            return ServiceResult<bool>.Failure(authorization.Error!, authorization.ErrorCode);
        }

        var image = await dbContext.AttractionImages.SingleOrDefaultAsync(candidate =>
            candidate.Id == imageId && candidate.AttractionId == attractionId,
            cancellationToken);
        if (image is null)
        {
            return ServiceResult<bool>.Failure("Image not found.", ServiceErrorCode.NotFound);
        }

        dbContext.AttractionImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private IQueryable<Attraction> GetAttractionQuery() => dbContext.Attractions
        .AsNoTracking()
        .Include(attraction => attraction.Category)
        .Include(attraction => attraction.Schedules)
        .Include(attraction => attraction.ExperienceSlots)
        .Include(attraction => attraction.Images)
        .AsSplitQuery();

    private async Task<ServiceResult<User>> RequireProviderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<User>.Failure("Provider account not found or inactive.", ServiceErrorCode.Forbidden);
        }

        return user.Role == UserRole.TourismProvider || user.Role == UserRole.Administrator
            ? ServiceResult<User>.Success(user)
            : ServiceResult<User>.Failure("Only tourism providers can manage attractions.", ServiceErrorCode.Forbidden);
    }

    private async Task<ServiceResult<User>> RequireTouristAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        return user is { IsActive: true, Role: UserRole.Tourist }
            ? ServiceResult<User>.Success(user)
            : ServiceResult<User>.Failure("Only active tourists can manage favorites.", ServiceErrorCode.Forbidden);
    }

    private async Task<ServiceResult<bool>> RequireOwnerOrAdministratorAsync(
        Attraction attraction,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var actor = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == actorId, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            return ServiceResult<bool>.Failure("User account not found or inactive.", ServiceErrorCode.Forbidden);
        }

        if (actor.Role == UserRole.Administrator ||
            (actor.Role == UserRole.TourismProvider && attraction.ProviderId == actorId))
        {
            return ServiceResult<bool>.Success(true);
        }

        return ServiceResult<bool>.Failure("You are not allowed to manage this attraction.", ServiceErrorCode.Forbidden);
    }

    private static AttractionResponse ToResponse(Attraction attraction, bool isFavorite) => new(
        attraction.Id,
        attraction.ProviderId,
        attraction.CategoryId,
        attraction.Name,
        attraction.Description,
        attraction.District,
        attraction.Address,
        attraction.Latitude,
        attraction.Longitude,
        attraction.Price,
        attraction.Status,
        attraction.IsActive,
        attraction.CreatedAt,
        attraction.UpdatedAt,
        new CategoryResponse(attraction.Category.Id, attraction.Category.Name, attraction.Category.Description),
        attraction.Schedules.OrderBy(schedule => schedule.DayOfWeek).Select(ToResponse).ToList(),
        attraction.ExperienceSlots.OrderBy(slot => slot.Date).ThenBy(slot => slot.StartTime).Select(ToResponse).ToList(),
        attraction.Images.OrderBy(image => image.SortOrder).Select(ToResponse).ToList(),
        isFavorite);

    private static AttractionScheduleResponse ToResponse(AttractionSchedule schedule) => new(
        schedule.Id,
        schedule.AttractionId,
        schedule.DayOfWeek,
        schedule.OpeningTime,
        schedule.ClosingTime,
        schedule.IsClosed);

    private static ExperienceSlotResponse ToResponse(ExperienceSlot slot) => new(
        slot.Id,
        slot.AttractionId,
        slot.Date,
        slot.StartTime,
        slot.EndTime,
        slot.Capacity,
        slot.AvailableCapacity);

    private static AttractionImageResponse ToResponse(AttractionImage image) => new(
        image.Id,
        image.AttractionId,
        image.ImageUrl,
        image.AltText,
        image.SortOrder,
        image.CreatedAt);
}
