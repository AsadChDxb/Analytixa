using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Settings;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISystemSettingsService _systemSettingsService;

    public SettingsController(ISystemSettingsService systemSettingsService)
    {
        _systemSettingsService = systemSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<SystemSettingsDto>>> Get(CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.GetSettingsAsync(cancellationToken);
        return Ok(ApiResponse<SystemSettingsDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPut("branding")]
    public async Task<ActionResult<ApiResponse<BrandingSettingsDto>>> UpdateBranding([FromBody] UpdateBrandingSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.UpdateBrandingSettingsAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<BrandingSettingsDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPut("datasource-connection")]
    public async Task<ActionResult<ApiResponse<DatasourceSettingsDto>>> UpdateDatasourceConnection([FromBody] UpdateDatasourceSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.UpdateDatasourceSettingsAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<DatasourceSettingsDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPut("ai-chat")]
    public async Task<ActionResult<ApiResponse<AiChatSettingsDto>>> UpdateAiChat([FromBody] UpdateAiChatSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.UpdateAiChatSettingsAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<AiChatSettingsDto>.Ok(result));
    }

    [Authorize(Policy = "ManageDatasource")]
    [HttpPost("datasource-connection/test")]
    public async Task<ActionResult<ApiResponse<DatasourceConnectionTestResultDto>>> TestDatasourceConnection([FromBody] TestDatasourceConnectionRequest request, CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.TestDatasourceConnectionAsync(request, cancellationToken);
        return Ok(ApiResponse<DatasourceConnectionTestResultDto>.Ok(result));
    }
}