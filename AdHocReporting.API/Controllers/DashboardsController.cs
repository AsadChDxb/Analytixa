using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Dashboards;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboards")]
public sealed class DashboardsController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<DashboardDto>>>> My([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50000, CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetMyDashboardsAsync(User.GetUserId(), pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<DashboardDto>>.Ok(result));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<DashboardDefinitionDto>>> Get(long id, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetDashboardDefinitionAsync(User.GetUserId(), id, cancellationToken);
        return Ok(ApiResponse<DashboardDefinitionDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Create([FromBody] CreateDashboardRequest request, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.CreateDashboardAsync(User.GetUserId(), request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<DashboardDto>.Ok(result));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Update(long id, [FromBody] UpdateDashboardRequest request, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.UpdateDashboardAsync(User.GetUserId(), id, request, User.HasRole("Admin"), User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<DashboardDto>.Ok(result));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _dashboardService.DeleteDashboardAsync(User.GetUserId(), id, User.HasRole("Admin"), User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Deleted"));
    }
}