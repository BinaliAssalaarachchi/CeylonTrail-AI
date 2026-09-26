using System.Security.Claims;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/attractions")]
public sealed class AttractionsController(IAttractionService attractionService, IDestinationAgentService destinationAgentService) : ControllerBase
{
    private const string ProviderOrAdministrator = "TourismProvider,Administrator";

    [HttpPost("recommendations")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> Recommend(
        DestinationRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await destinationAgentService.RecommendAsync(request, cancellationToken);
        return result.ServiceUnavailable
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Error })
            : result.Error is not null
                ? BadRequest(new { message = result.Error })
                : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> Create(
        CreateAttractionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var providerId))
        {
            return Unauthorized();
        }

        var result = await attractionService.CreateAsync(request, providerId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));
    }

    [HttpPatch("{id:guid}/approve")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await attractionService.ApproveAsync(id, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPatch("{id:guid}/reject")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectAttractionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.RejectAsync(id, request.Reason, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateAttractionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.SetStatusAsync(id, request.Status, request.Reason, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] AttractionSearchRequest request,
        CancellationToken cancellationToken) => SearchCore(request, cancellationToken);

    [HttpGet("search")]
    public Task<IActionResult> SearchExplicit(
        [FromQuery] AttractionSearchRequest request,
        CancellationToken cancellationToken) => SearchCore(request, cancellationToken);

    [HttpGet("mine")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> Mine(
        [FromQuery] AttractionSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var providerId))
        {
            return Unauthorized();
        }

        var result = await attractionService.GetMineAsync(request, providerId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("favorites")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> Favorites(
        [FromQuery] AttractionSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await attractionService.GetFavoritesAsync(request, touristId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var result = await attractionService.GetCategoriesAsync(cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Pending(
        [FromQuery] AttractionSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.GetPendingAsync(request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> AdminList(
        [FromQuery] AttractionSearchRequest request,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.GetAdminAttractionsAsync(request, status, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.GetByIdAsync(
            id,
            GetOptionalCurrentUserId(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateAttractionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.UpdateAsync(id, request, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.DeleteAsync(id, actorId, cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> GetAvailability(
        Guid id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.GetAvailabilityAsync(id, date, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPost("{id:guid}/schedules")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> AddSchedule(
        Guid id,
        CreateScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.AddScheduleAsync(id, request, actorId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id }, value));
    }

    [HttpPut("{id:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> UpdateSchedule(
        Guid id,
        Guid scheduleId,
        CreateScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.UpdateScheduleAsync(id, scheduleId, request, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpDelete("{id:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> DeleteSchedule(
        Guid id,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.DeleteScheduleAsync(id, scheduleId, actorId, cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    [HttpPost("{id:guid}/slots")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> AddSlot(
        Guid id,
        CreateExperienceSlotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.AddSlotAsync(id, request, actorId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetAvailability), new { id, date = value.Date }, value));
    }

    [HttpPut("{id:guid}/slots/{slotId:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> UpdateSlot(
        Guid id,
        Guid slotId,
        CreateExperienceSlotRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.UpdateSlotAsync(id, slotId, request, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpDelete("{id:guid}/slots/{slotId:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> DeleteSlot(
        Guid id,
        Guid slotId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.DeleteSlotAsync(id, slotId, actorId, cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    [HttpPost("{id:guid}/favorite")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> AddFavorite(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await attractionService.AddFavoriteAsync(id, touristId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id }, value));
    }

    [HttpDelete("{id:guid}/favorite")]
    [Authorize(Roles = "Tourist")]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await attractionService.RemoveFavoriteAsync(id, touristId, cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> AddImage(
        Guid id,
        AttractionImageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.AddImageAsync(id, request, actorId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id }, value));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> RemoveImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var result = await attractionService.RemoveImageAsync(id, imageId, actorId, cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    [HttpPatch("{id:guid}/activate")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
            return Unauthorized();

        var result = await attractionService.ActivateAsync(id, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPatch("{id:guid}/images/{imageId:guid}/primary")]
    [Authorize(Roles = ProviderOrAdministrator)]
    public async Task<IActionResult> SetPrimaryImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
            return Unauthorized();

        var result = await attractionService.SetPrimaryImageAsync(id, imageId, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    private async Task<IActionResult> SearchCore(
        AttractionSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attractionService.SearchAsync(
            request,
            GetOptionalCurrentUserId(),
            cancellationToken);

        return ToActionResult(result, Ok);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }

    private Guid? GetOptionalCurrentUserId()
    {
        return TryGetCurrentUserId(out var userId) ? userId : null;
    }

    private IActionResult ToActionResult<T>(
        ServiceResult<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded && result.Value is not null)
        {
            return onSuccess(result.Value);
        }

        return result.ErrorCode switch
        {
            ServiceErrorCode.NotFound => NotFound(new { message = result.Error }),
            ServiceErrorCode.Forbidden => Forbid(),
            ServiceErrorCode.Conflict => Conflict(new { message = result.Error }),
            ServiceErrorCode.Validation => BadRequest(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error ?? "The operation could not be completed." })
        };
    }
}
