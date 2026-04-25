using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.AI;
using AdHocReporting.Application.DTOs.Auth;
using AdHocReporting.Application.DTOs.Dashboards;
using AdHocReporting.Application.DTOs.Datasources;
using AdHocReporting.Application.DTOs.Reports;
using AdHocReporting.Application.DTOs.Settings;
using AdHocReporting.Application.DTOs.Users;

namespace AdHocReporting.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResultDto> RefreshAsync(RefreshTokenRequest request, string ipAddress, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<PaginatedResult<UserDto>> GetUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, string actor, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(long userId, UpdateUserRequest request, string actor, CancellationToken cancellationToken = default);
    Task SetUserActiveAsync(long userId, bool isActive, string actor, CancellationToken cancellationToken = default);
}

public interface IDatasourceService
{
    Task<PaginatedResult<DatasourceDto>> GetAllowedDatasourcesAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<DatasourceDto> CreateDatasourceAsync(CreateDatasourceRequest request, string actor, CancellationToken cancellationToken = default);
    Task<DatasourceDto> UpdateDatasourceAsync(long datasourceId, UpdateDatasourceRequest request, string actor, CancellationToken cancellationToken = default);
    Task DeleteDatasourceAsync(long datasourceId, string actor, CancellationToken cancellationToken = default);
    Task AssignRoleAccessAsync(AssignDatasourceRoleRequest request, string actor, CancellationToken cancellationToken = default);
    Task AssignUserAccessAsync(AssignDatasourceUserRequest request, string actor, CancellationToken cancellationToken = default);
    Task<DatasourceExecutionResult> RunDatasourceAsync(long userId, RunDatasourceRequest request, CancellationToken cancellationToken = default);
    Task<DatasourceExecutionResult> ExecuteAgentQueryAsync(long userId, long datasourceId, string selectQueryOnSource, Dictionary<string, object?> parameters, int pageSize = 200, CancellationToken cancellationToken = default);
    Task ValidateDatasourceDefinitionAsync(CreateDatasourceRequest request, CancellationToken cancellationToken = default);
    Task<DatasourceExecutionResult> TestDatasourceDefinitionAsync(TestDatasourceDefinitionRequest request, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<PaginatedResult<ReportDto>> GetMyReportsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PaginatedResult<ReportDto>> GetSharedReportsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ReportDefinitionDto> GetReportDefinitionAsync(long userId, long reportId, CancellationToken cancellationToken = default);
    Task<ReportDto> CreateReportAsync(long ownerUserId, CreateReportRequest request, string actor, CancellationToken cancellationToken = default);
    Task<ReportDto> UpdateReportAsync(long userId, long reportId, UpdateReportRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default);
    Task<ReportDto> CloneReportAsync(long userId, long reportId, string actor, CancellationToken cancellationToken = default);
    Task DeleteReportAsync(long userId, long reportId, bool isAdmin, string actor, CancellationToken cancellationToken = default);
    Task<ReportExecutionResult> RunReportAsync(long userId, ReportExecutionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAccessAsync(long userId, UpdateReportAccessRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<PaginatedResult<DashboardDto>> GetMyDashboardsAsync(long userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<DashboardDefinitionDto> GetDashboardDefinitionAsync(long userId, long dashboardId, CancellationToken cancellationToken = default);
    Task<DashboardDto> CreateDashboardAsync(long ownerUserId, CreateDashboardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<DashboardDto> UpdateDashboardAsync(long userId, long dashboardId, UpdateDashboardRequest request, bool isAdmin, string actor, CancellationToken cancellationToken = default);
    Task DeleteDashboardAsync(long userId, long dashboardId, bool isAdmin, string actor, CancellationToken cancellationToken = default);
}

public interface IExportService
{
    Task<byte[]> ExportReportToPdfAsync(long userId, long reportId, Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken = default);
    Task<byte[]> ExportReportToExcelAsync(long userId, long reportId, Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task LogAsync(long? userId, string action, string entityName, string? entityId, string? payloadSummary, string? ipAddress, string actor, CancellationToken cancellationToken = default);
}

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<BrandingSettingsDto> UpdateBrandingSettingsAsync(UpdateBrandingSettingsRequest request, string actor, CancellationToken cancellationToken = default);
    Task<DatasourceSettingsDto> UpdateDatasourceSettingsAsync(UpdateDatasourceSettingsRequest request, string actor, CancellationToken cancellationToken = default);
    Task<AiChatSettingsDto> UpdateAiChatSettingsAsync(UpdateAiChatSettingsRequest request, string actor, CancellationToken cancellationToken = default);
    Task<DatasourceConnectionTestResultDto> TestDatasourceConnectionAsync(TestDatasourceConnectionRequest request, CancellationToken cancellationToken = default);
}

public interface IAiChatService
{
    Task<IReadOnlyCollection<AiChatSessionSummaryDto>> GetSessionsAsync(long userId, int take = 12, CancellationToken cancellationToken = default);
    Task<AiChatSessionDetailDto> GetSessionAsync(long userId, long sessionId, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(long userId, long sessionId, string actor, CancellationToken cancellationToken = default);
    Task StreamReplyAsync(
        long userId,
        string actor,
        AiChatStreamRequest request,
        Func<AiChatStreamEventDto, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default);
}

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(long userId, string username, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, DateTime expiresAt);
    string GenerateRefreshToken();
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
