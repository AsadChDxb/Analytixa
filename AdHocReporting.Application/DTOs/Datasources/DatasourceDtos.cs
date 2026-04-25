using AdHocReporting.Domain.Enums;

namespace AdHocReporting.Application.DTOs.Datasources;

public record DatasourceParameterDto(string Name, string Label, string DataType, bool IsRequired, string? DefaultValue, string? OptionsJson);

public record DatasourceColumnDto(string ColumnName, string DataType, bool IsAllowed);

public record DatasourceDto(
    long Id,
    string Name,
    string Code,
    string Description,
    DatasourceType DatasourceType,
    string SqlDefinitionOrObjectName,
    bool IsActive,
    IReadOnlyCollection<DatasourceParameterDto> Parameters,
    IReadOnlyCollection<DatasourceColumnDto> AllowedColumns);

public record CreateDatasourceRequest(
    string Name,
    string Code,
    string Description,
    DatasourceType DatasourceType,
    string SqlDefinitionOrObjectName,
    string? ConnectionName,
    IReadOnlyCollection<DatasourceParameterDto> Parameters,
    IReadOnlyCollection<DatasourceColumnDto> AllowedColumns);

public record AssignDatasourceRoleRequest(long DatasourceId, long RoleId, bool CanView, bool CanUse, bool CanManage);

public record AssignDatasourceUserRequest(long DatasourceId, long UserId, bool CanView, bool CanUse, bool CanManage);

public record RunDatasourceRequest(long DatasourceId, Dictionary<string, object?> Parameters, int PageNumber = 1, int PageSize = 100);

public record DatasourceExecutionResult(IReadOnlyCollection<string> Columns, IReadOnlyCollection<Dictionary<string, object?>> Rows, int TotalCount);

public record TestDatasourceDefinitionRequest(
    DatasourceType DatasourceType,
    string SqlDefinitionOrObjectName,
    int PageSize = 10,
    Dictionary<string, object?>? Parameters = null);

public record UpdateDatasourceRequest(
    string Name,
    string Description,
    DatasourceType DatasourceType,
    string SqlDefinitionOrObjectName,
    IReadOnlyCollection<DatasourceParameterDto> Parameters,
    IReadOnlyCollection<DatasourceColumnDto> AllowedColumns);
