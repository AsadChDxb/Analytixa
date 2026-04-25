using AdHocReporting.Application.Common;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdHocDbContext _dbContext;

    public AdminController(AdHocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Authorize(Policy = "AdminLookup")]
    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<object>>> Roles(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Code, x.Description }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(roles));
    }

    [Authorize(Policy = "ManageRoles")]
    [HttpPost("roles")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (await _dbContext.Roles.AnyAsync(x => x.Code == code && !x.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Role code already exists.");
        }

        var role = new Domain.Entities.Role
        {
            Name = request.Name.Trim(),
            Code = code,
            Description = request.Description.Trim(),
            CreatedBy = User.Identity?.Name ?? "system"
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { role.Id, role.Name, role.Code, role.Description }));
    }

    [Authorize(Policy = "ManageRoles")]
    [HttpPut("roles/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRole(long id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Role not found.");

        var code = request.Code.Trim();
        if (await _dbContext.Roles.AnyAsync(x => x.Id != id && x.Code == code && !x.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Role code already exists.");
        }

        role.Name = request.Name.Trim();
        role.Code = code;
        role.Description = request.Description.Trim();
        role.IsActive = request.IsActive;
        role.ModifiedAt = DateTime.UtcNow;
        role.ModifiedBy = User.Identity?.Name ?? "system";

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { role.Id, role.Name, role.Code, role.Description, role.IsActive }));
    }

    [Authorize(Policy = "ManageRoles")]
    [HttpGet("roles/{id:long}/permissions")]
    public async Task<ActionResult<ApiResponse<object>>> GetRolePermissions(long id, CancellationToken cancellationToken)
    {
        var permissionIds = await _dbContext.RolePermissions
            .Where(x => x.RoleId == id && x.IsActive && !x.IsDeleted)
            .Select(x => x.PermissionId)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { roleId = id, permissionIds }));
    }

    [Authorize(Policy = "ManageRoles")]
    [HttpPut("roles/{id:long}/permissions")]
    public async Task<ActionResult<ApiResponse<string>>> UpdateRolePermissions(long id, [FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Role not found.");

        var validPermissionIds = await _dbContext.Permissions
            .Where(x => request.PermissionIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validPermissionIds.Count != request.PermissionIds.Distinct().Count())
        {
            throw new InvalidOperationException("One or more permission ids are invalid.");
        }

        var oldLinks = await _dbContext.RolePermissions.Where(x => x.RoleId == id).ToListAsync(cancellationToken);
        _dbContext.RolePermissions.RemoveRange(oldLinks);

        foreach (var permissionId in validPermissionIds.Distinct())
        {
            _dbContext.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
                CreatedBy = User.Identity?.Name ?? "system"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<string>.Ok("Role permissions updated"));
    }

    [Authorize(Policy = "AdminLookup")]
    [HttpGet("permissions")]
    public async Task<ActionResult<ApiResponse<object>>> Permissions(CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.Permissions.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Code, x.Description }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(permissions));
    }

    [Authorize(Policy = "AdminLookup")]
    [HttpGet("users-lite")]
    public async Task<ActionResult<ApiResponse<object>>> UsersLite(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.Username, x.FullName, x.Email })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(users));
    }

    [Authorize(Policy = "ViewAuditLogs")]
    [HttpGet("audit-logs")]
    public async Task<ActionResult<ApiResponse<object>>> AuditLogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs.Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(x => new
        {
            x.Id,
            x.UserId,
            x.Action,
            x.EntityName,
            x.EntityId,
            x.IpAddress,
            x.PayloadSummary,
            x.CreatedAt
        }).ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { total, pageNumber, pageSize, items }));
    }

    public sealed record CreateRoleRequest(string Name, string Code, string Description);

    public sealed record UpdateRoleRequest(string Name, string Code, string Description, bool IsActive);

    public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<long> PermissionIds);
}
