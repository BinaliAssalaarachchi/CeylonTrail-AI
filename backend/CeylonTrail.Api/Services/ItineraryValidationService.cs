using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ItineraryValidationService(ApplicationDbContext dbContext) : IItineraryValidationService
{
    public async Task<(bool Succeeded, string? Error, ItineraryValidationResponse? Response)> ValidateAsync(
        ItineraryValidationRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        if (authenticatedUserId == Guid.Empty || !await dbContext.Users.AnyAsync(
                user => user.Id == authenticatedUserId && user.IsActive,
                cancellationToken))
        {
            return (false, "The authenticated user does not exist or is inactive.", null);
        }

        var now = DateTime.UtcNow;
        var issues = new List<ValidationIssue>();
        var items = request.Items ?? new List<ItineraryItemRequest>();

        foreach (var item in items)
        {
            var start = ToUtc(item.StartDateTime);
            var end = ToUtc(item.EndDateTime);

            if (end <= start)
            {
                issues.Add(CreateIssue(
                    ValidationIssueType.InvalidTimeRange,
                    ValidationIssueSeverity.Critical,
                    "ITINERARY_INVALID_TIME_RANGE",
                    true,
                    $"Item '{DisplayReference(item)}' must end after it starts.",
                    item.District,
                    item.Reference,
                    now));
            }
        }

        var validItems = items
            .Select(item => new { Item = item, Start = ToUtc(item.StartDateTime), End = ToUtc(item.EndDateTime) })
            .Where(item => item.End > item.Start)
            .OrderBy(item => item.Start)
            .ToList();

        for (var index = 0; index < validItems.Count; index++)
        {
            for (var nextIndex = index + 1; nextIndex < validItems.Count; nextIndex++)
            {
                var current = validItems[index];
                var next = validItems[nextIndex];

                if (next.Start >= current.End)
                {
                    break;
                }

                issues.Add(CreateIssue(
                    ValidationIssueType.ScheduleConflict,
                    ValidationIssueSeverity.Critical,
                    "ITINERARY_SCHEDULE_OVERLAP",
                    true,
                    $"Items '{DisplayReference(current.Item)}' and '{DisplayReference(next.Item)}' overlap.",
                    current.Item.District,
                    $"{DisplayReference(current.Item)}, {DisplayReference(next.Item)}",
                    now));
            }
        }

        if (request.Budget.HasValue && request.EstimatedCost.HasValue && request.EstimatedCost > request.Budget)
        {
            var exceededBy = request.EstimatedCost.Value - request.Budget.Value;
            issues.Add(CreateIssue(
                ValidationIssueType.BudgetExceeded,
                ValidationIssueSeverity.High,
                "ITINERARY_BUDGET_EXCEEDED",
                true,
                $"Estimated cost exceeds the budget by {exceededBy:0.00}.",
                null,
                null,
                now));
        }

        var activeAlerts = await dbContext.TravelAlerts
            .AsNoTracking()
            .Where(alert => alert.Status == TravelAlertStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var item in validItems)
        {
            var district = item.Item.District.Trim();
            var matchingAlerts = activeAlerts.Where(alert =>
                string.Equals(alert.District.Trim(), district, StringComparison.OrdinalIgnoreCase) &&
                alert.StartDateTime < item.End &&
                alert.EndDateTime > item.Start);

            foreach (var alert in matchingAlerts)
            {
                var severity = ToValidationSeverity(alert.Severity);
                // Critical alerts make the itinerary infeasible; high alerts remain actionable warnings.
                var isBlocking = alert.Severity == TravelAlertSeverity.Critical;
                issues.Add(CreateIssue(
                    ValidationIssueType.TravelAlert,
                    severity,
                    "TRAVEL_ALERT_AFFECTS_ITINERARY",
                    isBlocking,
                    $"{alert.Severity} travel alert '{alert.Title}' affects '{DisplayReference(item.Item)}' in {alert.District}.",
                    alert.District,
                    item.Item.Reference,
                    now));
            }
        }

        var result = new Models.ValidationResult
        {
            Id = Guid.NewGuid(),
            TripReference = TrimToNull(request.TripReference),
            OverallStatus = CalculateOverallStatus(issues),
            RiskLevel = CalculateRiskLevel(issues),
            IsFeasible = issues.All(issue => !issue.IsBlocking),
            TotalIssueCount = issues.Count,
            BlockingIssueCount = issues.Count(issue => issue.IsBlocking),
            CreatedByUserId = authenticatedUserId,
            CreatedAt = now,
            Issues = issues
        };

        dbContext.ValidationResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, ToResponse(result));
    }

    public async Task<ItineraryValidationResponse?> GetByIdAsync(
        Guid id,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.ValidationResults
            .AsNoTracking()
            .Include(validation => validation.Issues)
            .SingleOrDefaultAsync(
                validation => validation.Id == id && validation.CreatedByUserId == authenticatedUserId,
                cancellationToken);

        return result is null ? null : ToResponse(result);
    }

    private static ValidationIssue CreateIssue(
        ValidationIssueType issueType,
        ValidationIssueSeverity severity,
        string ruleCode,
        bool isBlocking,
        string message,
        string? district,
        string? itemReference,
        DateTime createdAt) => new()
        {
            Id = Guid.NewGuid(),
            IssueType = issueType,
            Severity = severity,
            RuleCode = ruleCode,
            IsBlocking = isBlocking,
            Message = message,
            RelatedDistrict = TrimToNull(district),
            RelatedItemReference = TrimToNull(itemReference),
            CreatedAt = createdAt
        };

    private static ValidationOverallStatus CalculateOverallStatus(IReadOnlyCollection<ValidationIssue> issues) =>
        // Any blocking rule makes the itinerary Invalid; non-blocking issues produce Warning.
        issues.Any(issue => issue.IsBlocking)
            ? ValidationOverallStatus.Invalid
            : issues.Count > 0
                ? ValidationOverallStatus.Warning
                : ValidationOverallStatus.Valid;

    private static ValidationRiskLevel CalculateRiskLevel(IReadOnlyCollection<ValidationIssue> issues) =>
        issues.Count == 0
            ? ValidationRiskLevel.Low
            : (ValidationRiskLevel)issues.Max(issue => (int)issue.Severity);

    private static ValidationIssueSeverity ToValidationSeverity(TravelAlertSeverity severity) =>
        (ValidationIssueSeverity)severity;

    private static string DisplayReference(ItineraryItemRequest item) =>
        string.IsNullOrWhiteSpace(item.Reference) ? item.Title.Trim() : item.Reference.Trim();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static ItineraryValidationResponse ToResponse(Models.ValidationResult result) => new()
    {
        Id = result.Id,
        TripReference = result.TripReference,
        OverallStatus = result.OverallStatus,
        RiskLevel = result.RiskLevel,
        IsFeasible = result.IsFeasible,
        TotalIssueCount = result.TotalIssueCount,
        BlockingIssueCount = result.BlockingIssueCount,
        CreatedAt = result.CreatedAt,
        Issues = result.Issues
            .OrderBy(issue => issue.CreatedAt)
            .ThenBy(issue => issue.Id)
            .Select(issue => new ValidationIssueResponse
            {
                Id = issue.Id,
                IssueType = issue.IssueType,
                Severity = issue.Severity,
                Message = issue.Message,
                RuleCode = issue.RuleCode,
                IsBlocking = issue.IsBlocking,
                RelatedDistrict = issue.RelatedDistrict,
                RelatedItemReference = issue.RelatedItemReference,
                CreatedAt = issue.CreatedAt
            })
            .ToList()
    };
}
