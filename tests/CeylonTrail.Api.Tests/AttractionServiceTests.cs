using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class AttractionServiceTests
{
    [Fact]
    public async Task ProviderCreatesPendingAttractionAndCanViewItPrivately()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var created = await service.CreateAsync(CreateRequest(category.Id), provider.Id);

        Assert.True(created.Succeeded);
        Assert.Equal("PendingApproval", created.Value!.Status);
        Assert.Equal(provider.Id, created.Value.ProviderId);

        var privateDetails = await service.GetByIdAsync(created.Value.Id, provider.Id);
        var publicDetails = await service.GetByIdAsync(created.Value.Id);

        Assert.True(privateDetails.Succeeded);
        Assert.False(publicDetails.Succeeded);
        Assert.Equal(ServiceErrorCode.NotFound, publicDetails.ErrorCode);
    }

    [Fact]
    public async Task ProviderCannotModifyAnotherProvidersAttraction()
    {
        await using var dbContext = CreateDbContext();
        var owner = AddUser(dbContext, UserRole.TourismProvider);
        var otherProvider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var created = await service.CreateAsync(CreateRequest(category.Id), owner.Id);
        var result = await service.UpdateAsync(
            created.Value!.Id,
            new UpdateAttractionRequest
            {
                CategoryId = category.Id,
                Name = "Unauthorized update",
                Description = "Not allowed",
                District = "Kandy",
                Address = "Address",
                Latitude = 7.29m,
                Longitude = 80.63m,
                Price = 10m
            },
            otherProvider.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task InactiveAttractionRejectsManagementOperations()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Name = "Inactive attraction",
            Description = "Inactive",
            District = "Kandy",
            Address = "Address",
            Latitude = 7.29m,
            Longitude = 80.63m,
            Price = 10m,
            Status = "PendingApproval",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Attractions.Add(attraction);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var result = await service.AddSlotAsync(
            attraction.Id,
            new CreateExperienceSlotRequest
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                Capacity = 10,
                AvailableCapacity = 10
            },
            provider.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorCode.Validation, result.ErrorCode);
    }

    [Fact]
    public async Task ApprovedActiveAttractionIsDiscoverableAndFavoritesAreTouristScoped()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var tourist = AddUser(dbContext, UserRole.Tourist);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var created = await service.CreateAsync(CreateRequest(category.Id), provider.Id);
        await service.ApproveAsync(created.Value!.Id);
        var favorite = await service.AddFavoriteAsync(created.Value.Id, tourist.Id);
        var favorites = await service.GetFavoritesAsync(new AttractionSearchRequest(), tourist.Id);
        var search = await service.SearchAsync(new AttractionSearchRequest());

        Assert.True(favorite.Succeeded);
        Assert.True(favorites.Succeeded);
        Assert.Single(favorites.Value!.Items);
        Assert.Single(search.Value!.Items);
    }

    [Fact]
    public async Task CategoriesAreReturnedAsCategoryDtos()
    {
        await using var dbContext = CreateDbContext();
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var result = await service.GetCategoriesAsync();

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!, item => item.Id == category.Id && item.Name == "Culture");
    }

    [Fact]
    public async Task PendingAttractionsCanBeListedForAdministratorWorkflow()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var pending = await service.CreateAsync(CreateRequest(category.Id), provider.Id);
        await service.ApproveAsync(pending.Value!.Id);

        var secondPending = await service.CreateAsync(CreateRequest(category.Id), provider.Id);
        var result = await service.GetPendingAsync(new AttractionSearchRequest());

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Items);
        Assert.Equal(secondPending.Value!.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task AdminCanRejectAttractionWithReasonAndProviderCanResubmit()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var created = await service.CreateAsync(CreateRequest(category.Id), provider.Id);
        var rejection = await service.RejectAsync(created.Value!.Id, "Please provide clearer address details and more descriptive images.");

        Assert.True(rejection.Succeeded);
        Assert.Equal("Rejected", rejection.Value!.Status);
        Assert.Equal("Please provide clearer address details and more descriptive images.", rejection.Value.RejectionReason);

        // Provider updates the attraction - should automatically resubmit to PendingApproval
        var updateResult = await service.UpdateAsync(
            created.Value.Id,
            new UpdateAttractionRequest
            {
                CategoryId = category.Id,
                Name = "Updated Temple attraction",
                Description = "A refined cultural attraction description",
                District = "Kandy",
                Address = "Exact Street No 4, Temple Road",
                Latitude = 7.2906m,
                Longitude = 80.6337m,
                Price = 30m
            },
            provider.Id);

        Assert.True(updateResult.Succeeded);
        Assert.Equal("PendingApproval", updateResult.Value!.Status);

        // Admin can list all attractions with status filter
        var adminAll = await service.GetAdminAttractionsAsync(new AttractionSearchRequest(), "all");
        var adminPending = await service.GetAdminAttractionsAsync(new AttractionSearchRequest(), "PendingApproval");
        var adminRejected = await service.GetAdminAttractionsAsync(new AttractionSearchRequest(), "Rejected");

        Assert.True(adminAll.Succeeded);
        Assert.Single(adminAll.Value!.Items);
        Assert.Single(adminPending.Value!.Items);
        Assert.Empty(adminRejected.Value!.Items);
    }

    [Fact]
    public async Task ProviderCanSelectOnePrimaryImageAndDeletingItPromotesFallback()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);
        var created = await service.CreateAsync(CreateRequest(category.Id), provider.Id);

        var first = await service.AddImageAsync(created.Value!.Id, new AttractionImageRequest
        {
            ImageUrl = "https://example.com/first.jpg",
            SortOrder = 0
        }, provider.Id);
        var second = await service.AddImageAsync(created.Value.Id, new AttractionImageRequest
        {
            ImageUrl = "https://example.com/second.jpg",
            SortOrder = 1
        }, provider.Id);

        Assert.True(first.Succeeded);
        Assert.True(first.Value!.IsPrimary);
        Assert.False(second.Value!.IsPrimary);

        var selected = await service.SetPrimaryImageAsync(created.Value.Id, second.Value.Id, provider.Id);
        var afterSelection = await service.GetByIdAsync(created.Value.Id, provider.Id);

        Assert.True(selected.Succeeded);
        Assert.False(afterSelection.Value!.Images.Single(image => image.Id == first.Value.Id).IsPrimary);
        Assert.True(afterSelection.Value.Images.Single(image => image.Id == second.Value.Id).IsPrimary);

        var removed = await service.RemoveImageAsync(created.Value.Id, second.Value.Id, provider.Id);
        var afterRemoval = await service.GetByIdAsync(created.Value.Id, provider.Id);

        Assert.True(removed.Succeeded);
        Assert.True(afterRemoval.Value!.Images.Single().IsPrimary);
    }

    [Fact]
    public async Task ProviderCanPermanentlyDeleteAttractionFromDatabase()
    {
        await using var dbContext = CreateDbContext();
        var provider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);

        var created = await service.CreateAsync(CreateRequest(category.Id), provider.Id);
        Assert.True(created.Succeeded);

        var deleteResult = await service.DeleteAsync(created.Value!.Id, provider.Id);
        Assert.True(deleteResult.Succeeded);

        // Attraction record should no longer exist in dbContext
        var exists = await dbContext.Attractions.AnyAsync(a => a.Id == created.Value.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task ProviderCannotSelectAnImageOnAnotherProvidersAttraction()
    {
        await using var dbContext = CreateDbContext();
        var owner = AddUser(dbContext, UserRole.TourismProvider);
        var otherProvider = AddUser(dbContext, UserRole.TourismProvider);
        var category = AddCategory(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new AttractionService(dbContext);
        var created = await service.CreateAsync(CreateRequest(category.Id), owner.Id);
        var image = await service.AddImageAsync(created.Value!.Id, new AttractionImageRequest
        {
            ImageUrl = "https://example.com/owned.jpg",
            SortOrder = 0
        }, owner.Id);

        var result = await service.SetPrimaryImageAsync(created.Value.Id, image.Value!.Id, otherProvider.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorCode.Forbidden, result.ErrorCode);
    }

    private static CreateAttractionRequest CreateRequest(Guid categoryId) => new()
    {
        CategoryId = categoryId,
        Name = "Temple attraction",
        Description = "A cultural attraction",
        District = "Kandy",
        Address = "Temple Road",
        Latitude = 7.2906m,
        Longitude = 80.6337m,
        Price = 25m
    };

    private static User AddUser(ApplicationDbContext dbContext, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = role.ToString(),
            LastName = "User",
            Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static Category AddCategory(ApplicationDbContext dbContext)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Culture",
            Description = "Cultural attractions"
        };
        dbContext.Categories.Add(category);
        return category;
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
