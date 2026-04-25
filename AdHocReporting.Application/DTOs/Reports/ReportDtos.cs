namespace AdHocReporting.Application.DTOs.Reports;

public record ReportColumnDto(string ColumnName, string DisplayName, int DisplayOrder);
public record ReportFilterDto(string FieldName, string Operator, string? Value, string ValueType);
public record ReportSortDto(string FieldName, string Direction, int SortOrder);
public record ReportGroupDto(string FieldName, int GroupOrder);
public record ReportAggregationDto(string FieldName, string AggregateFunction);
public record ReportParameterDto(string Name, string? Value, string DataType);

public record ReportBrandingDto(
    string? LogoUrl,
    string Title,
    string? Subtitle,
    string? HeaderFieldsJson,
    string HeaderAlignment,
    bool ShowLogo,
    bool ShowGeneratedDate,
    bool ShowGeneratedBy,
    string? FooterText,
    string? WatermarkText);

public record CreateReportRequest(
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    bool IsPublic,
    bool IsPrivate,
    IReadOnlyCollection<ReportColumnDto> Columns,
    IReadOnlyCollection<ReportFilterDto> Filters,
    IReadOnlyCollection<ReportSortDto> Sorts,
    IReadOnlyCollection<ReportGroupDto> Groups,
    IReadOnlyCollection<ReportAggregationDto> Aggregations,
    IReadOnlyCollection<ReportParameterDto> Parameters,
    ReportBrandingDto Branding);

public record UpdateReportRequest(
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    bool IsPublic,
    bool IsPrivate,
    IReadOnlyCollection<ReportColumnDto> Columns,
    IReadOnlyCollection<ReportFilterDto> Filters,
    IReadOnlyCollection<ReportSortDto> Sorts,
    IReadOnlyCollection<ReportGroupDto> Groups,
    IReadOnlyCollection<ReportAggregationDto> Aggregations,
    IReadOnlyCollection<ReportParameterDto> Parameters,
    ReportBrandingDto Branding);

public record UpdateReportAccessRequest(long ReportId, IReadOnlyCollection<long> RoleIds, IReadOnlyCollection<long> UserIds, bool IsPublic, bool IsPrivate);

public record ReportDto(long Id, string Name, string Code, string Description, long DatasourceId, long OwnerUserId, bool IsPublic, bool IsPrivate);

public record ReportDefinitionDto(
    long Id,
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    long OwnerUserId,
    bool IsPublic,
    bool IsPrivate,
    IReadOnlyCollection<ReportColumnDto> Columns,
    IReadOnlyCollection<ReportFilterDto> Filters,
    IReadOnlyCollection<ReportSortDto> Sorts,
    IReadOnlyCollection<ReportGroupDto> Groups,
    IReadOnlyCollection<ReportAggregationDto> Aggregations,
    IReadOnlyCollection<ReportParameterDto> Parameters,
    ReportBrandingDto Branding);

public record ReportExecutionRequest(long ReportId, Dictionary<string, object?> RuntimeParameters, int PageNumber = 1, int PageSize = 100);

public record ReportExecutionResult(IReadOnlyCollection<string> Columns, IReadOnlyCollection<Dictionary<string, object?>> Rows, int TotalCount);
