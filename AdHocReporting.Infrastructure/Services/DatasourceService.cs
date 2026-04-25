using System.Data;
using System.Data.Common;
using System.Text.Json;
using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Datasources;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Domain.Enums;
using AdHocReporting.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class DatasourceService : IDatasourceService
{
    private const string DefaultConnectionName = "DefaultConnection";
    private const string DatasourceSettingsCategory = "Datasource";
    private const string ExternalConnectionStringSettingKey = "ExternalConnectionString";
    private readonly AdHocDbContext _dbContext;
    private readonly SettingsSecretProtectionService _settingsSecretProtectionService;

    public DatasourceService(AdHocDbContext dbContext, SettingsSecretProtectionService settingsSecretProtectionService)
    {
        _dbContext = dbContext;
        _settingsSecretProtectionService = settingsSecretProtectionService;
    }

    public async Task<PaginatedResult<DatasourceDto>> GetAllowedDatasourcesAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var roleIds = await _dbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(cancellationToken);
        var isAdmin = await _dbContext.UserRoles.AnyAsync(x => x.UserId == userId && x.Role!.Code == "Admin" && x.IsActive && !x.IsDeleted, cancellationToken);

        var query = _dbContext.Datasources
            .Include(x => x.Parameters)
            .Include(x => x.AllowedColumns)
            .AsSplitQuery()
            .Where(x => x.IsActive && !x.IsDeleted);

        if (!isAdmin)
        {
            query = query.Where(x =>
                x.UserAccesses.Any(ua => ua.UserId == userId && ua.CanView && ua.IsActive && !ua.IsDeleted) ||
                x.RoleAccesses.Any(ra => roleIds.Contains(ra.RoleId) && ra.CanView && ra.IsActive && !ra.IsDeleted));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PaginatedResult<DatasourceDto>
        {
            Items = items.Select(ToDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<DatasourceDto> CreateDatasourceAsync(CreateDatasourceRequest request, string actor, CancellationToken cancellationToken = default)
    {
        await ValidateDatasourceDefinitionAsync(request, cancellationToken);

        if (await _dbContext.Datasources.AnyAsync(x => x.Code == request.Code, cancellationToken))
        {
            throw new InvalidOperationException("Datasource code already exists.");
        }

        var datasource = new Datasource
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            DatasourceType = request.DatasourceType,
            SqlDefinitionOrObjectName = request.SqlDefinitionOrObjectName,
            ConnectionName = DefaultConnectionName,
            CreatedBy = actor,
            Parameters = request.Parameters.Select(x => new DatasourceParameter
            {
                Name = x.Name,
                Label = x.Label,
                DataType = x.DataType,
                IsRequired = x.IsRequired,
                DefaultValue = x.DefaultValue,
                OptionsJson = x.OptionsJson,
                CreatedBy = actor
            }).ToList(),
            AllowedColumns = request.AllowedColumns.Select(x => new DatasourceColumnMetadata
            {
                ColumnName = x.ColumnName,
                DataType = x.DataType,
                IsAllowed = x.IsAllowed,
                CreatedBy = actor
            }).ToList()
        };

        if (datasource.AllowedColumns.Count == 0 && (datasource.DatasourceType == DatasourceType.Query || datasource.DatasourceType == DatasourceType.View))
        {
            var inferredColumns = await InferColumnsForDefinitionAsync(datasource.DatasourceType, datasource.SqlDefinitionOrObjectName, cancellationToken);
            datasource.AllowedColumns = inferredColumns
                .Select(columnName => new DatasourceColumnMetadata
                {
                    ColumnName = columnName,
                    DataType = "string",
                    IsAllowed = true,
                    CreatedBy = actor
                })
                .ToList();
        }

        _dbContext.Datasources.Add(datasource);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(datasource);
    }

    public async Task<DatasourceDto> UpdateDatasourceAsync(long datasourceId, UpdateDatasourceRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var datasource = await _dbContext.Datasources
            .Include(x => x.Parameters)
            .Include(x => x.AllowedColumns)
            .FirstOrDefaultAsync(x => x.Id == datasourceId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Datasource {datasourceId} not found.");

        datasource.Name = request.Name;
        datasource.Description = request.Description;
        datasource.DatasourceType = request.DatasourceType;
        datasource.SqlDefinitionOrObjectName = request.SqlDefinitionOrObjectName;
        datasource.ModifiedBy = actor;
        datasource.ModifiedAt = DateTime.UtcNow;

        // Replace columns
        _dbContext.RemoveRange(datasource.AllowedColumns);
        datasource.AllowedColumns = request.AllowedColumns.Select(c => new DatasourceColumnMetadata
        {
            ColumnName = c.ColumnName,
            DataType = c.DataType,
            IsAllowed = c.IsAllowed,
            CreatedBy = actor
        }).ToList();

        // Replace parameters
        _dbContext.RemoveRange(datasource.Parameters);
        datasource.Parameters = request.Parameters.Select(p => new DatasourceParameter
        {
            Name = p.Name,
            Label = p.Label,
            DataType = p.DataType,
            IsRequired = p.IsRequired,
            DefaultValue = p.DefaultValue,
            OptionsJson = p.OptionsJson,
            CreatedBy = actor
        }).ToList();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(datasource);
    }

    public async Task DeleteDatasourceAsync(long datasourceId, string actor, CancellationToken cancellationToken = default)
    {
        var datasource = await _dbContext.Datasources
            .FirstOrDefaultAsync(x => x.Id == datasourceId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Datasource {datasourceId} not found.");

        datasource.IsDeleted = true;
        datasource.IsActive = false;
        datasource.ModifiedBy = actor;
        datasource.ModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignRoleAccessAsync(AssignDatasourceRoleRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.DatasourceRoleAccess
            .FirstOrDefaultAsync(x => x.DatasourceId == request.DatasourceId && x.RoleId == request.RoleId, cancellationToken);

        if (existing is null)
        {
            _dbContext.DatasourceRoleAccess.Add(new DatasourceRoleAccess
            {
                DatasourceId = request.DatasourceId,
                RoleId = request.RoleId,
                CanView = request.CanView,
                CanUse = request.CanUse,
                CanManage = request.CanManage,
                CreatedBy = actor
            });
        }
        else
        {
            existing.CanView = request.CanView;
            existing.CanUse = request.CanUse;
            existing.CanManage = request.CanManage;
            existing.ModifiedAt = DateTime.UtcNow;
            existing.ModifiedBy = actor;
            existing.IsActive = true;
            existing.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignUserAccessAsync(AssignDatasourceUserRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.DatasourceUserAccess
            .FirstOrDefaultAsync(x => x.DatasourceId == request.DatasourceId && x.UserId == request.UserId, cancellationToken);

        if (existing is null)
        {
            _dbContext.DatasourceUserAccess.Add(new DatasourceUserAccess
            {
                DatasourceId = request.DatasourceId,
                UserId = request.UserId,
                CanView = request.CanView,
                CanUse = request.CanUse,
                CanManage = request.CanManage,
                CreatedBy = actor
            });
        }
        else
        {
            existing.CanView = request.CanView;
            existing.CanUse = request.CanUse;
            existing.CanManage = request.CanManage;
            existing.ModifiedAt = DateTime.UtcNow;
            existing.ModifiedBy = actor;
            existing.IsActive = true;
            existing.IsDeleted = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DatasourceExecutionResult> RunDatasourceAsync(long userId, RunDatasourceRequest request, CancellationToken cancellationToken = default)
    {
        var roleIds = await _dbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(cancellationToken);
        var isAdmin = await _dbContext.UserRoles.AnyAsync(x => x.UserId == userId && x.Role!.Code == "Admin" && x.IsActive && !x.IsDeleted, cancellationToken);
        var datasource = await _dbContext.Datasources
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.Id == request.DatasourceId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Datasource not found.");

        var hasAccess = isAdmin || await _dbContext.DatasourceUserAccess.AnyAsync(x => x.DatasourceId == datasource.Id && x.UserId == userId && x.CanUse && x.IsActive && !x.IsDeleted, cancellationToken)
            || await _dbContext.DatasourceRoleAccess.AnyAsync(x => x.DatasourceId == datasource.Id && roleIds.Contains(x.RoleId) && x.CanUse && x.IsActive && !x.IsDeleted, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Datasource access denied.");
        }

        SqlSafetyValidator.ValidateDefinition(datasource.DatasourceType, datasource.SqlDefinitionOrObjectName);

        var (dbConn, shouldDisposeConnection) = await GetDatasourceExecutionConnectionAsync(cancellationToken);

        try
        {
            var requestParameters = request.Parameters ?? new Dictionary<string, object?>();
            var requestParametersIgnoreCase = requestParameters
                .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

            var dynamicParameters = new DynamicParameters();
            foreach (var parameter in datasource.Parameters)
            {
                if (requestParametersIgnoreCase.TryGetValue(parameter.Name, out var value))
                {
                    dynamicParameters.Add("@" + parameter.Name, NormalizeParameterValue(value, parameter.DataType));
                }
                else if (parameter.IsRequired)
                {
                    var fallback = BuildFallbackRequiredValue(parameter);
                    if (fallback is null)
                    {
                        throw new InvalidOperationException($"Missing required parameter: {parameter.Name}");
                    }

                    dynamicParameters.Add("@" + parameter.Name, fallback);
                }
                else
                {
                    dynamicParameters.Add("@" + parameter.Name, NormalizeParameterValue(parameter.DefaultValue, parameter.DataType));
                }
            }

            IReadOnlyCollection<Dictionary<string, object?>> rows;
            if (datasource.DatasourceType == DatasourceType.Query)
            {
                rows = await ExecuteQueryDatasourceAsync(
                    dbConn,
                    datasource.SqlDefinitionOrObjectName,
                    dynamicParameters,
                    request.PageNumber,
                    request.PageSize);
            }
            else if (datasource.DatasourceType == DatasourceType.View)
            {
                IEnumerable<dynamic> records;
                var sql = $"SELECT * FROM [{datasource.SqlDefinitionOrObjectName}] ORDER BY (SELECT NULL) OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                dynamicParameters.Add("@Offset", Math.Max(0, (request.PageNumber - 1) * request.PageSize));
                dynamicParameters.Add("@PageSize", request.PageSize);
                records = await dbConn.QueryAsync(sql, dynamicParameters);
                rows = records
                    .Select(r => (IDictionary<string, object>)r)
                    .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
                    .ToList();
            }
            else
            {
                IEnumerable<dynamic> records;
                records = await dbConn.QueryAsync(datasource.SqlDefinitionOrObjectName, dynamicParameters, commandType: CommandType.StoredProcedure);
                rows = records
                    .Select(r => (IDictionary<string, object>)r)
                    .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
                    .ToList();
            }
            var columns = rows.Count == 0 ? new List<string>() : rows.First().Keys.ToList();

            return new DatasourceExecutionResult(columns, rows, rows.Count);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await dbConn.DisposeAsync();
            }
        }
    }

    public async Task<DatasourceExecutionResult> ExecuteAgentQueryAsync(
        long userId,
        long datasourceId,
        string selectQueryOnSource,
        Dictionary<string, object?> parameters,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        SqlSafetyValidator.ValidateAgentSelectQuery(selectQueryOnSource);

        var roleIds = await _dbContext.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(cancellationToken);
        var isAdmin = await _dbContext.UserRoles.AnyAsync(x => x.UserId == userId && x.Role!.Code == "Admin" && x.IsActive && !x.IsDeleted, cancellationToken);
        var datasource = await _dbContext.Datasources
            .Include(x => x.Parameters)
            .Include(x => x.AllowedColumns)
            .FirstOrDefaultAsync(x => x.Id == datasourceId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Datasource not found.");

        if (datasource.DatasourceType == DatasourceType.StoredProcedure)
        {
            throw new InvalidOperationException("Agent query execution is not supported for stored procedure datasources.");
        }

        var hasAccess = isAdmin || await _dbContext.DatasourceUserAccess.AnyAsync(x => x.DatasourceId == datasource.Id && x.UserId == userId && x.CanUse && x.IsActive && !x.IsDeleted, cancellationToken)
            || await _dbContext.DatasourceRoleAccess.AnyAsync(x => x.DatasourceId == datasource.Id && roleIds.Contains(x.RoleId) && x.CanUse && x.IsActive && !x.IsDeleted, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Datasource access denied.");
        }

        var sourceCte = datasource.DatasourceType == DatasourceType.Query
            ? datasource.SqlDefinitionOrObjectName
            : $"SELECT * FROM [{datasource.SqlDefinitionOrObjectName}]";

        var executableSql = $"WITH src AS ({sourceCte})\n{selectQueryOnSource}";

        var (dbConn, shouldDisposeConnection) = await GetDatasourceExecutionConnectionAsync(cancellationToken);
        try
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@__take", Math.Clamp(pageSize, 1, 1000));

            foreach (var (key, value) in parameters)
            {
                dynamicParameters.Add("@" + key, value);
            }

            var records = await dbConn.QueryAsync(executableSql, dynamicParameters);
            var rows = records
                .Select(r => (IDictionary<string, object>)r)
                .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
                .ToList();
            var columns = rows.Count == 0 ? new List<string>() : rows.First().Keys.ToList();

            return new DatasourceExecutionResult(columns, rows, rows.Count);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await dbConn.DisposeAsync();
            }
        }
    }

    public Task ValidateDatasourceDefinitionAsync(CreateDatasourceRequest request, CancellationToken cancellationToken = default)
    {
        SqlSafetyValidator.ValidateDefinition(request.DatasourceType, request.SqlDefinitionOrObjectName);
        return Task.CompletedTask;
    }

    public async Task<DatasourceExecutionResult> TestDatasourceDefinitionAsync(TestDatasourceDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        SqlSafetyValidator.ValidateDefinition(request.DatasourceType, request.SqlDefinitionOrObjectName);

        var (dbConn, shouldDisposeConnection) = await GetDatasourceExecutionConnectionAsync(cancellationToken);

        try
        {
            var pageSize = Math.Clamp(request.PageSize, 1, 200);
            var dynamicParameters = new DynamicParameters();
            if (request.Parameters is not null)
            {
                foreach (var (name, value) in request.Parameters)
                {
                    dynamicParameters.Add("@" + name, NormalizeParameterValue(value, null));
                }
            }

            IReadOnlyCollection<Dictionary<string, object?>> rows;
            if (request.DatasourceType == DatasourceType.Query)
            {
                rows = await ExecuteQueryDatasourceAsync(
                    dbConn,
                    request.SqlDefinitionOrObjectName,
                    dynamicParameters,
                    1,
                    pageSize);
            }
            else if (request.DatasourceType == DatasourceType.View)
            {
                IEnumerable<dynamic> records;
                var sql = $"SELECT * FROM [{request.SqlDefinitionOrObjectName}] ORDER BY (SELECT NULL) OFFSET 0 ROWS FETCH NEXT @PageSize ROWS ONLY";
                dynamicParameters.Add("@PageSize", pageSize);
                records = await dbConn.QueryAsync(sql, dynamicParameters);
                rows = records
                    .Select(r => (IDictionary<string, object>)r)
                    .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
                    .ToList();
            }
            else
            {
                IEnumerable<dynamic> records;
                records = await dbConn.QueryAsync(request.SqlDefinitionOrObjectName, dynamicParameters, commandType: CommandType.StoredProcedure);
                records = records.Take(pageSize).ToList();
                rows = records
                    .Select(r => (IDictionary<string, object>)r)
                    .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
                    .ToList();
            }
            var columns = rows.Count == 0 ? new List<string>() : rows.First().Keys.ToList();

            return new DatasourceExecutionResult(columns, rows, rows.Count);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await dbConn.DisposeAsync();
            }
        }
    }

    private async Task<List<string>> InferColumnsForDefinitionAsync(DatasourceType datasourceType, string definition, CancellationToken cancellationToken)
    {
        var (dbConn, shouldDisposeConnection) = await GetDatasourceExecutionConnectionAsync(cancellationToken);

        try
        {
            if (datasourceType == DatasourceType.Query)
            {
                var inferredColumns = await TryInferQueryColumnsAsync(dbConn, definition, cancellationToken);
                if (inferredColumns.Count > 0)
                {
                    return inferredColumns;
                }
            }
            else
            {
                var schemaRows = await dbConn.QueryAsync($"SELECT TOP 0 * FROM [{definition}]");
                var firstRow = schemaRows.FirstOrDefault() as IDictionary<string, object>;
                if (firstRow is not null)
                {
                    return firstRow.Keys.ToList();
                }
            }

            var preview = await TestDatasourceDefinitionAsync(new TestDatasourceDefinitionRequest(datasourceType, definition, 1), cancellationToken);
            return preview.Columns.ToList();
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await dbConn.DisposeAsync();
            }
        }
    }

    private async Task<(DbConnection Connection, bool ShouldDispose)> GetDatasourceExecutionConnectionAsync(CancellationToken cancellationToken)
    {
        var storedExternalConnectionString = await _dbContext.SystemSettings
            .Where(x => x.Category == DatasourceSettingsCategory && x.SettingKey == ExternalConnectionStringSettingKey && x.IsActive && !x.IsDeleted)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        var externalConnectionString = _settingsSecretProtectionService.Unprotect(storedExternalConnectionString);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            externalConnectionString = NormalizeConnectionString(externalConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(storedExternalConnectionString) && string.IsNullOrWhiteSpace(externalConnectionString))
        {
            throw new InvalidOperationException("External datasource connection is configured but could not be decrypted.");
        }

        if (string.IsNullOrWhiteSpace(externalConnectionString))
        {
            var defaultConnection = _dbContext.Database.GetDbConnection();
            if (defaultConnection.State != ConnectionState.Open)
            {
                await defaultConnection.OpenAsync(cancellationToken);
            }

            return (defaultConnection, false);
        }

        var externalConnection = new SqlConnection(externalConnectionString);
        await externalConnection.OpenAsync(cancellationToken);
        return (externalConnection, true);
    }

    private static string NormalizeConnectionString(string raw)
    {
        var normalized = raw.Trim();
        while (normalized.Contains("\\\\", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string NormalizeQueryDefinition(string definition)
    {
        var normalized = definition.Trim();
        while (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized;
    }

    private static bool IsCteQuery(string definition)
        => definition.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase);

    private static List<Dictionary<string, object?>> MaterializeRows(IEnumerable<dynamic> records)
        => records
            .Select(r => (IDictionary<string, object>)r)
            .Select(r => r.ToDictionary(k => k.Key, k => (object?)k.Value))
            .ToList();

    private static async Task<List<string>> TryInferQueryColumnsAsync(DbConnection dbConn, string definition, CancellationToken cancellationToken)
    {
        await using var command = dbConn.CreateCommand();
        command.CommandText = NormalizeQueryDefinition(definition);
        command.CommandType = CommandType.Text;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken);
            return reader.GetColumnSchema()
                .Select(column => column.ColumnName)
                .Where(columnName => !string.IsNullOrWhiteSpace(columnName))
                .Cast<string>()
                .ToList();
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyCollection<Dictionary<string, object?>>> ExecuteQueryDatasourceAsync(
        DbConnection dbConn,
        string definition,
        DynamicParameters dynamicParameters,
        int pageNumber,
        int pageSize)
    {
        var normalizedDefinition = NormalizeQueryDefinition(definition);
        if (!IsCteQuery(normalizedDefinition))
        {
            var sql = $"SELECT * FROM ({normalizedDefinition}) q ORDER BY (SELECT NULL) OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            dynamicParameters.Add("@Offset", Math.Max(0, (pageNumber - 1) * pageSize));
            dynamicParameters.Add("@PageSize", pageSize);
            var records = await dbConn.QueryAsync(sql, dynamicParameters);
            return MaterializeRows(records);
        }

        var cteRecords = await dbConn.QueryAsync(normalizedDefinition, dynamicParameters);
        var cteRows = MaterializeRows(cteRecords);
        var offset = Math.Max(0, (pageNumber - 1) * pageSize);
        return cteRows.Skip(offset).Take(pageSize).ToList();
    }

    private static DatasourceDto ToDto(Datasource x)
    {
        return new DatasourceDto(
            x.Id,
            x.Name,
            x.Code,
            x.Description,
            x.DatasourceType,
            x.SqlDefinitionOrObjectName,
            x.IsActive,
            x.Parameters.Select(p => new DatasourceParameterDto(p.Name, p.Label, p.DataType, p.IsRequired, p.DefaultValue, p.OptionsJson)).ToList(),
            x.AllowedColumns.Select(c => new DatasourceColumnDto(c.ColumnName, c.DataType, c.IsAllowed)).ToList());
    }

    private static object? NormalizeParameterValue(object? value, string? dataType)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement json)
        {
            value = json.ValueKind switch
            {
                JsonValueKind.String => json.GetString(),
                JsonValueKind.Number => json.TryGetInt64(out var int64Value) ? int64Value : json.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => json.ToString()
            };
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalizedType = (dataType ?? string.Empty).Trim().ToLowerInvariant();

            if ((normalizedType == "date" || normalizedType == "datetime") && DateTime.TryParse(text, out var parsedDate))
            {
                return parsedDate;
            }

            if ((normalizedType == "number" || normalizedType == "int" || normalizedType == "decimal") && decimal.TryParse(text, out var parsedNumber))
            {
                return parsedNumber;
            }

            if (normalizedType == "boolean" && bool.TryParse(text, out var parsedBool))
            {
                return parsedBool;
            }

            return text;
        }

        return value;
    }

    private static object? BuildFallbackRequiredValue(DatasourceParameter parameter)
    {
        var normalizedType = (parameter.DataType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedType == "date" || normalizedType == "datetime")
        {
            if (parameter.Name.Contains("start", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.UtcNow.Date.AddDays(-30);
            }

            if (parameter.Name.Contains("end", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.UtcNow.Date;
            }

            return DateTime.UtcNow.Date;
        }

        return NormalizeParameterValue(parameter.DefaultValue, parameter.DataType);
    }
}
