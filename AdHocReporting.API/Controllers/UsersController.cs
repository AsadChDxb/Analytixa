using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Users;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize(Policy = "ManageUsers")]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetUsersAsync(pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PaginatedResult<UserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.CreateUserAsync(request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(result));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateUserAsync(id, request, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<UserDto>.Ok(result));
    }

    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateStatus(long id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        await _userService.SetUserActiveAsync(id, isActive, User.GetUsername(), cancellationToken);
        return Ok(ApiResponse<string>.Ok("Updated"));
    }
}
