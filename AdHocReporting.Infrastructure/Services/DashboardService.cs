using System.Text.Json;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Dashboards;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly AdHocDbContext _dbContext;

    public DashboardService(AdHocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<DashboardDto>> GetMyDashboardsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Dashboards
            .Where(x => x.OwnerUserId == userId && x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<DashboardDto>
        {
            Items = items.Select(ToDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<DashboardDefinitionDto> GetDashboardDefinitionAsync(long userId, long dashboardId, CancellationToken cancellationToken = default)
    {
        var dashboard = await _dbContext.Dashboards
            .FirstOrDefaultAsync(x => x.Id == dashboardId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Dashboard not found.");

        if (dashboard.OwnerUserId != userId)
        {
            throw new UnauthorizedAccessException("Dashboard access denied.");
        }

        return ToDefinitionDto(dashboard);
    }

    public async Task<DashboardDto> CreateDashboardAsync(long ownerUserId, CreateDashboardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var codeExists = await _dbContext.Dashboards.AnyAsync(x => x.Code == request.Code, cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException("Dashboard code already exists. Please use a different code.");
        }

        var dashboard = new Dashboard
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            DatasourceId = request.DatasourceId,
            OwnerUserId = ownerUserId,
            DefinitionJson = NormalizeDefinition(request.Definition),
            CreatedBy = actor
        };

        _dbContext.Dashboards.Add(dashboard);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(dashboard);
    }

    public async Task<DashboardDto> UpdateDashboardAsync(long userId, long dashboardId, UpdateDashboardRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default)
    {
        var dashboard = await _dbContext.Dashboards
            .FirstOrDefaultAsync(x => x.Id == dashboardId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Dashboard not found.");

        if (dashboard.OwnerUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only owner or admin can update dashboard.");
        }

        if (!string.Equals(dashboard.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _dbContext.Dashboards.AnyAsync(x => x.Id != dashboardId && x.Code == request.Code, cancellationToken);
            if (codeExists)
            {
                throw new InvalidOperationException("Dashboard code already exists. Please use a different code.");
            }
        }

        dashboard.Name = request.Name;
        dashboard.Code = request.Code;
        dashboard.Description = request.Description;
        dashboard.DatasourceId = request.DatasourceId;
        dashboard.DefinitionJson = NormalizeDefinition(request.Definition);
        dashboard.ModifiedAt = DateTime.UtcNow;
        dashboard.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(dashboard);
    }

    public async Task DeleteDashboardAsync(long userId, long dashboardId, bool isAdmin, string actor, CancellationToken cancellationToken = default)
    {
        var dashboard = await _dbContext.Dashboards
            .FirstOrDefaultAsync(x => x.Id == dashboardId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Dashboard not found.");

        if (dashboard.OwnerUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only owner or admin can delete dashboard.");
        }

        dashboard.IsDeleted = true;
        dashboard.IsActive = false;
        dashboard.ModifiedAt = DateTime.UtcNow;
        dashboard.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DashboardDto ToDto(Dashboard dashboard) =>
        new(dashboard.Id, dashboard.Name, dashboard.Code, dashboard.Description, dashboard.DatasourceId, dashboard.OwnerUserId);

    private static DashboardDefinitionDto ToDefinitionDto(Dashboard dashboard) =>
        new(dashboard.Id, dashboard.Name, dashboard.Code, dashboard.Description, dashboard.DatasourceId, dashboard.OwnerUserId, ParseDefinition(dashboard.DefinitionJson));

    private static string NormalizeDefinition(JsonElement definition)
    {
        return definition.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? "{}"
            : definition.GetRawText();
    }

    private static JsonElement ParseDefinition(string? definitionJson)
    {
        var json = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}