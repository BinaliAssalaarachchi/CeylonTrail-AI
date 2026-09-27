using CeylonTrail.Api.DTOs.Reports;
using CeylonTrail.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "TravelCoordinator,Administrator")]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ReportOverviewResponse>> GetOverview(CancellationToken cancellationToken) =>
        Ok(await reportService.GetOverviewAsync(cancellationToken));
}
