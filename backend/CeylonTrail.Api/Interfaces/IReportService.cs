using CeylonTrail.Api.DTOs.Reports;

namespace CeylonTrail.Api.Interfaces;

public interface IReportService
{
    Task<ReportOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}
