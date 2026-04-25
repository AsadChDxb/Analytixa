namespace AdHocReporting.Application.DTOs.AI;

public record AiChatStreamRequest(long? SessionId, string Message);

public record AiChatMessageDto(
    long Id,
    string Role,
    string Content,
    DateTime CreatedAt,
    string? MetadataJson = null);

public record AiChatSessionSummaryDto(
    long Id,
    string Title,
    DateTime UpdatedAt,
    string LastMessagePreview);

public record AiChatSessionDetailDto(
    long Id,
    string Title,
    DateTime UpdatedAt,
    IReadOnlyCollection<AiChatMessageDto> Messages);

public record AiChatStreamEventDto(
    string Type,
    string? Content = null,
    long? SessionId = null,
    string? SessionTitle = null,
    AiChatTraceDto? Trace = null);

public record AiChatTraceDto(
    string? DatasourceName,
    string? DatasourceCode,
    string? DatasourcePurpose,
    string? PlannerReason,
    string? QueryPlanReason,
    decimal? QueryConfidence,
    string? GeneratedSql,
    bool UsedAgentQuery,
    string? PlannerModel,
    string? ResponderModel);