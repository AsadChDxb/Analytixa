using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Reports;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ReportDto>>>> My([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50000, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetMyReportsAsync(User.GetUserId(), pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<ReportDto>>.Ok(result));
    }

    [HttpGet("shared")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ReportDto>>>> Shared([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50000, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetSharedReportsAsync(User.GetUserId(), pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<ReportDto>>.Ok(result));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<ReportDefinitionDto>>> Get(long id, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetReportDefinitionAsync(User.GetUserId(), id, cancellationToken);
        return Ok(ApiResponse<ReportDefinitionDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReportDto>>> Create([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        var result = await _reportService.CreateReportAsync(User.GetUserId(), request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<ReportDto>.Ok(result));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<ReportDto>>> Update(long id, [FromBody] UpdateReportRequest request, CancellationToken cancellationToken)
    {
        var result = await _reportService.UpdateReportAsync(User.GetUserId(), id, request, User.HasRole("Admin"), User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<ReportDto>.Ok(result));
    }

    [HttpPost("{id:long}/clone")]
    public async Task<ActionResult<ApiResponse<ReportDto>>> Clone(long id, CancellationToken cancellationToken)
    {
        var result = await _reportService.CloneReportAsync(User.GetUserId(), id, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<ReportDto>.Ok(result));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _reportService.DeleteReportAsync(User.GetUserId(), id, User.HasRole("Admin"), User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Deleted"));
    }

    [Authorize(Policy = "RunReport")]
    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<ReportExecutionResult>>> Run([FromBody] ReportExecutionRequest request, CancellationToken cancellationToken)
    {
        var result = await _reportService.RunReportAsync(User.GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<ReportExecutionResult>.Ok(result));
    }

    [HttpPost("access")]
    public async Task<ActionResult<ApiResponse<string>>> Access([FromBody] UpdateReportAccessRequest request, CancellationToken cancellationToken)
    {
        await _reportService.UpdateAccessAsync(User.GetUserId(), request, User.HasRole("Admin"), User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Access updated"));
    }
}
