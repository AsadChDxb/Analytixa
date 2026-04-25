using System.Text.Json;

namespace AdHocReporting.Application.DTOs.Dashboards;

public record CreateDashboardRequest(
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    JsonElement Definition);

public record UpdateDashboardRequest(
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    JsonElement Definition);

public record DashboardDto(long Id, string Name, string Code, string Description, long DatasourceId, long OwnerUserId);

public record DashboardDefinitionDto(
    long Id,
    string Name,
    string Code,
    string Description,
    long DatasourceId,
    long OwnerUserId,
    JsonElement Definition);