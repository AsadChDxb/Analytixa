using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Datasources;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/datasources")]
public sealed class DatasourcesController : ControllerBase
{
    private readonly IDatasourceService _datasourceService;

    public DatasourcesController(IDatasourceService datasourceService)
    {
        _datasourceService = datasourceService;
    }

    [HttpGet("allowed")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<DatasourceDto>>>> GetAllowed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _datasourceService.GetAllowedDatasourcesAsync(User.GetUserId(), pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<DatasourceDto>>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<DatasourceDto>>> Update(long id, [FromBody] UpdateDatasourceRequest request, CancellationToken cancellationToken)
    {
        var result = await _datasourceService.UpdateDatasourceAsync(id, request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<DatasourceDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _datasourceService.DeleteDatasourceAsync(id, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Datasource deleted."));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DatasourceDto>>> Create([FromBody] CreateDatasourceRequest request, CancellationToken cancellationToken)
    {
        var result = await _datasourceService.CreateDatasourceAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<DatasourceDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<string>>> Validate([FromBody] CreateDatasourceRequest request, CancellationToken cancellationToken)
    {
        await _datasourceService.ValidateDatasourceDefinitionAsync(request, cancellationToken);
        return Ok(ApiResponse<string>.Ok("Valid definition"));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost("test-definition")]
    public async Task<ActionResult<ApiResponse<DatasourceExecutionResult>>> TestDefinition([FromBody] TestDatasourceDefinitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _datasourceService.TestDatasourceDefinitionAsync(request, cancellationToken);
        return Ok(ApiResponse<DatasourceExecutionResult>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost("assign-role")]
    public async Task<ActionResult<ApiResponse<string>>> AssignRole([FromBody] AssignDatasourceRoleRequest request, CancellationToken cancellationToken)
    {
        await _datasourceService.AssignRoleAccessAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Role assigned"));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost("assign-user")]
    public async Task<ActionResult<ApiResponse<string>>> AssignUser([FromBody] AssignDatasourceUserRequest request, CancellationToken cancellationToken)
    {
        await _datasourceService.AssignUserAccessAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("User assigned"));
    }

    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<DatasourceExecutionResult>>> Run([FromBody] RunDatasourceRequest request, CancellationToken cancellationToken)
    {
        var result = await _datasourceService.RunDatasourceAsync(User.GetUserId(), request, cancellationToken);
        return Ok(ApiResponse<DatasourceExecutionResult>.Ok(result));
    }
}
