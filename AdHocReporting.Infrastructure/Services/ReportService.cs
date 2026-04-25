using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Datasources;
using AdHocReporting.Application.DTOs.Reports;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    private const string BrandingSettingsCategory = "Branding";
    private const string CompanyLogoSettingKey = "CompanyLogoDataUrl";

    private readonly AdHocDbContext _dbContext;
    private readonly IDatasourceService _datasourceService;

    public ReportService(AdHocDbContext dbContext, IDatasourceService datasourceService)
    {
        _dbContext = dbContext;
        _datasourceService = datasourceService;
    }

    public async Task<PaginatedResult<ReportDto>> GetMyReportsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reports.Where(x => x.OwnerUserId == userId && x.IsActive && !x.IsDeleted).OrderByDescending(x => x.Id);
        return await PageReportQuery(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<PaginatedResult<ReportDto>> GetSharedReportsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var roleIds = await _dbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(cancellationToken);

        var query = _dbContext.Reports.Where(x =>
            x.IsActive && !x.IsDeleted &&
            (x.IsPublic ||
             x.OwnerUserId == userId ||
             x.UserAccesses.Any(ua => ua.UserId == userId && ua.CanView && ua.IsActive && !ua.IsDeleted) ||
             x.RoleAccesses.Any(ra => roleIds.Contains(ra.RoleId) && ra.CanView && ra.IsActive && !ra.IsDeleted)))
            .OrderByDescending(x => x.Id);

        return await PageReportQuery(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<ReportDefinitionDto> GetReportDefinitionAsync(long userId, long reportId, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports
            .Include(x => x.Columns)
            .Include(x => x.Filters)
            .Include(x => x.Sorts)
            .Include(x => x.Groups)
            .Include(x => x.Aggregations)
            .Include(x => x.Parameters)
            .Include(x => x.Branding)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        var allowedReports = await GetSharedReportsAsync(userId, 1, int.MaxValue, cancellationToken);
        if (!allowedReports.Items.Any(x => x.Id == report.Id))
        {
            throw new UnauthorizedAccessException("Report access denied.");
        }

        var defaultLogo = await GetDefaultLogoUrlAsync(cancellationToken);
        return ToDefinitionDto(report, defaultLogo);
    }

    public async Task<ReportDto> CreateReportAsync(long ownerUserId, CreateReportRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var codeExists = await _dbContext.Reports.AnyAsync(x => x.Code == request.Code, cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException("Report code already exists. Please use a different code.");
        }

        var defaultLogo = await GetDefaultLogoUrlAsync(cancellationToken);
        var report = new Report
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            DatasourceId = request.DatasourceId,
            OwnerUserId = ownerUserId,
            IsPublic = request.IsPublic,
            IsPrivate = request.IsPrivate,
            CreatedBy = actor,
            Columns = request.Columns.Select(x => new ReportColumn { ColumnName = x.ColumnName, DisplayName = x.DisplayName, DisplayOrder = x.DisplayOrder, CreatedBy = actor }).ToList(),
            Filters = request.Filters.Select(x => new ReportFilter { FieldName = x.FieldName, Operator = x.Operator, Value = x.Value, ValueType = x.ValueType, CreatedBy = actor }).ToList(),
            Sorts = request.Sorts.Select(x => new ReportSort { FieldName = x.FieldName, Direction = x.Direction, SortOrder = x.SortOrder, CreatedBy = actor }).ToList(),
            Groups = request.Groups.Select(x => new ReportGroup { FieldName = x.FieldName, GroupOrder = x.GroupOrder, CreatedBy = actor }).ToList(),
            Aggregations = request.Aggregations.Select(x => new ReportAggregation { FieldName = x.FieldName, AggregateFunction = x.AggregateFunction, CreatedBy = actor }).ToList(),
            Parameters = request.Parameters.Select(x => new ReportParameter { Name = x.Name, Value = x.Value, DataType = x.DataType, CreatedBy = actor }).ToList(),
            Branding = new ReportBranding
            {
                LogoUrl = defaultLogo,
                Title = request.Branding.Title,
                Subtitle = request.Branding.Subtitle,
                HeaderFieldsJson = request.Branding.HeaderFieldsJson,
                HeaderAlignment = request.Branding.HeaderAlignment,
                ShowLogo = request.Branding.ShowLogo,
                ShowGeneratedDate = request.Branding.ShowGeneratedDate,
                ShowGeneratedBy = request.Branding.ShowGeneratedBy,
                FooterText = request.Branding.FooterText,
                WatermarkText = request.Branding.WatermarkText,
                CreatedBy = actor
            }
        };

        _dbContext.Reports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ReportDto(report.Id, report.Name, report.Code, report.Description, report.DatasourceId, report.OwnerUserId, report.IsPublic, report.IsPrivate);
    }

    public async Task<ReportDto> UpdateReportAsync(long userId, long reportId, UpdateReportRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default)
    {
        var defaultLogo = await GetDefaultLogoUrlAsync(cancellationToken);
        var report = await _dbContext.Reports
            .Include(x => x.Columns)
            .Include(x => x.Filters)
            .Include(x => x.Sorts)
            .Include(x => x.Groups)
            .Include(x => x.Aggregations)
            .Include(x => x.Parameters)
            .Include(x => x.Branding)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        if (report.OwnerUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only owner or admin can update report.");
        }

        if (!string.Equals(report.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _dbContext.Reports.AnyAsync(x => x.Id != reportId && x.Code == request.Code, cancellationToken);
            if (codeExists)
            {
                throw new InvalidOperationException("Report code already exists. Please use a different code.");
            }
        }

        report.Name = request.Name;
        report.Code = request.Code;
        report.Description = request.Description;
        report.DatasourceId = request.DatasourceId;
        report.IsPublic = request.IsPublic;
        report.IsPrivate = request.IsPrivate;
        report.ModifiedAt = DateTime.UtcNow;
        report.ModifiedBy = actor;

        _dbContext.ReportColumns.RemoveRange(report.Columns);
        _dbContext.ReportFilters.RemoveRange(report.Filters);
        _dbContext.ReportSorts.RemoveRange(report.Sorts);
        _dbContext.ReportGroups.RemoveRange(report.Groups);
        _dbContext.ReportAggregations.RemoveRange(report.Aggregations);
        _dbContext.ReportParameters.RemoveRange(report.Parameters);

        report.Columns = request.Columns.Select(x => new ReportColumn
        {
            ReportId = report.Id,
            ColumnName = x.ColumnName,
            DisplayName = x.DisplayName,
            DisplayOrder = x.DisplayOrder,
            CreatedBy = actor
        }).ToList();

        report.Filters = request.Filters.Select(x => new ReportFilter
        {
            ReportId = report.Id,
            FieldName = x.FieldName,
            Operator = x.Operator,
            Value = x.Value,
            ValueType = x.ValueType,
            CreatedBy = actor
        }).ToList();

        report.Sorts = request.Sorts.Select(x => new ReportSort
        {
            ReportId = report.Id,
            FieldName = x.FieldName,
            Direction = x.Direction,
            SortOrder = x.SortOrder,
            CreatedBy = actor
        }).ToList();

        report.Groups = request.Groups.Select(x => new ReportGroup
        {
            ReportId = report.Id,
            FieldName = x.FieldName,
            GroupOrder = x.GroupOrder,
            CreatedBy = actor
        }).ToList();

        report.Aggregations = request.Aggregations.Select(x => new ReportAggregation
        {
            ReportId = report.Id,
            FieldName = x.FieldName,
            AggregateFunction = x.AggregateFunction,
            CreatedBy = actor
        }).ToList();

        report.Parameters = request.Parameters.Select(x => new ReportParameter
        {
            ReportId = report.Id,
            Name = x.Name,
            Value = x.Value,
            DataType = x.DataType,
            CreatedBy = actor
        }).ToList();

        if (report.Branding is null)
        {
            report.Branding = new ReportBranding
            {
                ReportId = report.Id,
                CreatedBy = actor
            };
        }

        report.Branding.LogoUrl = defaultLogo;
        report.Branding.Title = request.Branding.Title;
        report.Branding.Subtitle = request.Branding.Subtitle;
        report.Branding.HeaderFieldsJson = request.Branding.HeaderFieldsJson;
        report.Branding.HeaderAlignment = request.Branding.HeaderAlignment;
        report.Branding.ShowLogo = request.Branding.ShowLogo;
        report.Branding.ShowGeneratedDate = request.Branding.ShowGeneratedDate;
        report.Branding.ShowGeneratedBy = request.Branding.ShowGeneratedBy;
        report.Branding.FooterText = request.Branding.FooterText;
        report.Branding.WatermarkText = request.Branding.WatermarkText;
        report.Branding.ModifiedAt = DateTime.UtcNow;
        report.Branding.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ReportDto(report.Id, report.Name, report.Code, report.Description, report.DatasourceId, report.OwnerUserId, report.IsPublic, report.IsPrivate);
    }

    public async Task<ReportDto> CloneReportAsync(long userId, long reportId, string actor, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.Reports
            .Include(x => x.Columns)
            .Include(x => x.Filters)
            .Include(x => x.Sorts)
            .Include(x => x.Groups)
            .Include(x => x.Aggregations)
            .Include(x => x.Parameters)
            .Include(x => x.Branding)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        var clone = new CreateReportRequest(
            source.Name + " (Clone)",
            source.Code + "_CLONE_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            source.Description,
            source.DatasourceId,
            false,
            true,
            source.Columns.Select(x => new ReportColumnDto(x.ColumnName, x.DisplayName, x.DisplayOrder)).ToList(),
            source.Filters.Select(x => new ReportFilterDto(x.FieldName, x.Operator, x.Value, x.ValueType)).ToList(),
            source.Sorts.Select(x => new ReportSortDto(x.FieldName, x.Direction, x.SortOrder)).ToList(),
            source.Groups.Select(x => new ReportGroupDto(x.FieldName, x.GroupOrder)).ToList(),
            source.Aggregations.Select(x => new ReportAggregationDto(x.FieldName, x.AggregateFunction)).ToList(),
            source.Parameters.Select(x => new ReportParameterDto(x.Name, x.Value, x.DataType)).ToList(),
            new ReportBrandingDto(source.Branding?.LogoUrl, source.Branding?.Title ?? source.Name, source.Branding?.Subtitle, source.Branding?.HeaderFieldsJson, source.Branding?.HeaderAlignment ?? "Left", source.Branding?.ShowLogo ?? false, source.Branding?.ShowGeneratedDate ?? true, source.Branding?.ShowGeneratedBy ?? true, source.Branding?.FooterText, source.Branding?.WatermarkText));

        return await CreateReportAsync(userId, clone, actor, cancellationToken);
    }

    public async Task DeleteReportAsync(long userId, long reportId, bool isAdmin, string actor, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        if (report.OwnerUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only owner or admin can delete report.");
        }

        report.IsDeleted = true;
        report.IsActive = false;
        report.ModifiedAt = DateTime.UtcNow;
        report.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReportExecutionResult> RunReportAsync(long userId, ReportExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.Id == request.ReportId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        var allowedReports = await GetSharedReportsAsync(userId, 1, int.MaxValue, cancellationToken);
        if (!allowedReports.Items.Any(x => x.Id == report.Id))
        {
            throw new UnauthorizedAccessException("Report access denied.");
        }

        var runRequest = new RunDatasourceRequest(report.DatasourceId, request.RuntimeParameters, request.PageNumber, request.PageSize);
        var dsResult = await _datasourceService.RunDatasourceAsync(userId, runRequest, cancellationToken);
        return new ReportExecutionResult(dsResult.Columns, dsResult.Rows, dsResult.TotalCount);
    }

    public async Task UpdateAccessAsync(long userId, UpdateReportAccessRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(x => x.Id == request.ReportId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Report not found.");

        if (report.OwnerUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only owner/admin can change access.");
        }

        var oldRoleAccesses = _dbContext.ReportRoleAccess.Where(x => x.ReportId == request.ReportId);
        var oldUserAccesses = _dbContext.ReportUserAccess.Where(x => x.ReportId == request.ReportId);
        _dbContext.ReportRoleAccess.RemoveRange(oldRoleAccesses);
        _dbContext.ReportUserAccess.RemoveRange(oldUserAccesses);

        foreach (var roleId in request.RoleIds.Distinct())
        {
            _dbContext.ReportRoleAccess.Add(new ReportRoleAccess { ReportId = request.ReportId, RoleId = roleId, CanView = true, CanRun = true, CanExport = true, CreatedBy = actor });
        }

        foreach (var targetUserId in request.UserIds.Distinct())
        {
            _dbContext.ReportUserAccess.Add(new ReportUserAccess { ReportId = request.ReportId, UserId = targetUserId, CanView = true, CanRun = true, CanExport = true, CreatedBy = actor });
        }

        report.IsPublic = request.IsPublic;
        report.IsPrivate = request.IsPrivate;
        report.ModifiedAt = DateTime.UtcNow;
        report.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<PaginatedResult<ReportDto>> PageReportQuery(IQueryable<Report> query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PaginatedResult<ReportDto>
        {
            Items = items.Select(x => new ReportDto(x.Id, x.Name, x.Code, x.Description, x.DatasourceId, x.OwnerUserId, x.IsPublic, x.IsPrivate)).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private static ReportDefinitionDto ToDefinitionDto(Report report, string? defaultLogo)
    {
        return new ReportDefinitionDto(
            report.Id,
            report.Name,
            report.Code,
            report.Description,
            report.DatasourceId,
            report.OwnerUserId,
            report.IsPublic,
            report.IsPrivate,
            report.Columns.OrderBy(x => x.DisplayOrder).Select(x => new ReportColumnDto(x.ColumnName, x.DisplayName, x.DisplayOrder)).ToList(),
            report.Filters.Select(x => new ReportFilterDto(x.FieldName, x.Operator, x.Value, x.ValueType)).ToList(),
            report.Sorts.OrderBy(x => x.SortOrder).Select(x => new ReportSortDto(x.FieldName, x.Direction, x.SortOrder)).ToList(),
            report.Groups.OrderBy(x => x.GroupOrder).Select(x => new ReportGroupDto(x.FieldName, x.GroupOrder)).ToList(),
            report.Aggregations.Select(x => new ReportAggregationDto(x.FieldName, x.AggregateFunction)).ToList(),
            report.Parameters.Select(x => new ReportParameterDto(x.Name, x.Value, x.DataType)).ToList(),
            new ReportBrandingDto(
                defaultLogo,
                report.Branding?.Title ?? report.Name,
                report.Branding?.Subtitle,
                report.Branding?.HeaderFieldsJson,
                report.Branding?.HeaderAlignment ?? "Left",
                report.Branding?.ShowLogo ?? false,
                report.Branding?.ShowGeneratedDate ?? true,
                report.Branding?.ShowGeneratedBy ?? true,
                report.Branding?.FooterText,
                report.Branding?.WatermarkText));
    }

    private async Task<string?> GetDefaultLogoUrlAsync(CancellationToken cancellationToken)
    {
        var value = await _dbContext.SystemSettings
            .Where(x => x.Category == BrandingSettingsCategory && x.SettingKey == CompanyLogoSettingKey && x.IsActive && !x.IsDeleted)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
