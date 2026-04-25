using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdHocReporting.Application.DTOs.AI;
using AdHocReporting.Application.DTOs.Datasources;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class AiChatService : IAiChatService
{
    private const string AiChatCategory = "AiChat";
    private const string NoDataMessage = "The required data is not available, so I cannot answer this question.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex ComputedExpressionTokenRegex = new("[A-Za-z_][A-Za-z0-9_]*|\\d+(?:\\.\\d+)?|[()+\\-*/]", RegexOptions.Compiled);
    private static readonly Regex IdentifierTokenRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex AliasRegex = new("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
    private static readonly Regex UnsupportedExpressionCharactersRegex = new("[^A-Za-z0-9_+\\-*/().\\s]", RegexOptions.Compiled);

    private readonly AdHocDbContext _dbContext;
    private readonly IDatasourceService _datasourceService;
    private readonly SettingsSecretProtectionService _settingsSecretProtectionService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiChatService(
        AdHocDbContext dbContext,
        IDatasourceService datasourceService,
        SettingsSecretProtectionService settingsSecretProtectionService,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _datasourceService = datasourceService;
        _settingsSecretProtectionService = settingsSecretProtectionService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyCollection<AiChatSessionSummaryDto>> GetSessionsAsync(long userId, int take = 12, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.AiChatSessions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.ModifiedAt ?? x.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .Select(x => new
            {
                x.Id,
                x.Title,
                UpdatedAt = x.ModifiedAt ?? x.CreatedAt,
                LastMessage = x.Messages
                    .Where(m => m.IsActive && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return sessions
            .Select(x => new AiChatSessionSummaryDto(
                x.Id,
                x.Title,
                x.UpdatedAt,
                BuildPreview(x.LastMessage)))
            .ToList();
    }

    public async Task<AiChatSessionDetailDto> GetSessionAsync(long userId, long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.AiChatSessions
            .AsNoTracking()
            .Include(x => x.Messages.Where(m => m.IsActive && !m.IsDeleted).OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Chat session not found.");

        return new AiChatSessionDetailDto(
            session.Id,
            session.Title,
            session.ModifiedAt ?? session.CreatedAt,
            session.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(x => new AiChatMessageDto(x.Id, x.Role, x.Content, x.CreatedAt, x.MetadataJson))
                .ToList());
    }

    public async Task DeleteSessionAsync(long userId, long sessionId, string actor, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.AiChatSessions
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Chat session not found.");

        session.IsActive = false;
        session.IsDeleted = true;
        session.ModifiedAt = DateTime.UtcNow;
        session.ModifiedBy = actor;

        foreach (var message in session.Messages.Where(m => m.IsActive && !m.IsDeleted))
        {
            message.IsActive = false;
            message.IsDeleted = true;
            message.ModifiedAt = DateTime.UtcNow;
            message.ModifiedBy = actor;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StreamReplyAsync(
        long userId,
        string actor,
        AiChatStreamRequest request,
        Func<AiChatStreamEventDto, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            await onEvent(new AiChatStreamEventDto("error", "Message is required."), cancellationToken);
            return;
        }

        var runtimeSettings = await GetRuntimeSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(runtimeSettings.ApiKey))
        {
            await onEvent(new AiChatStreamEventDto("error", "OpenAI API key is not configured in settings."), cancellationToken);
            return;
        }

        var session = await GetOrCreateSessionAsync(userId, actor, request, cancellationToken);
        await AppendMessageAsync(session.Id, "user", request.Message.Trim(), actor, null, cancellationToken);
        await onEvent(new AiChatStreamEventDto("meta", SessionId: session.Id, SessionTitle: session.Title), cancellationToken);

        try
        {
            var recentMessages = await _dbContext.AiChatMessages
                .AsNoTracking()
                .Where(x => x.SessionId == session.Id && x.IsActive && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Take(12)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            var accessibleDatasources = await _datasourceService.GetAllowedDatasourcesAsync(userId, 1, 200, cancellationToken);
            if (accessibleDatasources.Items.Count == 0)
            {
                await EmitFixedReplyAsync(session, actor, NoDataMessage, onEvent, cancellationToken);
                return;
            }

            var plannerDecision = await CreatePlannerDecisionAsync(runtimeSettings, recentMessages, accessibleDatasources.Items, request.Message.Trim(), cancellationToken);
            if (plannerDecision.DatasourceId is null)
            {
                await EmitFixedReplyAsync(session, actor, NoDataMessage, onEvent, cancellationToken);
                return;
            }

            var selectedDatasource = accessibleDatasources.Items.FirstOrDefault(x => x.Id == plannerDecision.DatasourceId.Value);
            if (selectedDatasource is null)
            {
                await EmitFixedReplyAsync(session, actor, NoDataMessage, onEvent, cancellationToken);
                return;
            }

            var queryPlan = await CreateQueryPlannerPlanAsync(runtimeSettings, recentMessages, selectedDatasource, request.Message.Trim(), cancellationToken);
            QueryBuildResult? generatedQuery = BuildAgentSelectQuery(selectedDatasource, queryPlan);

            DatasourceExecutionResult executionResult;
            if (generatedQuery is not null)
            {
                try
                {
                    executionResult = await _datasourceService.ExecuteAgentQueryAsync(
                        userId,
                        selectedDatasource.Id,
                        generatedQuery.Sql,
                        generatedQuery.Parameters,
                        generatedQuery.PageSize,
                        cancellationToken);
                }
                catch
                {
                    executionResult = await _datasourceService.RunDatasourceAsync(
                        userId,
                        new RunDatasourceRequest(plannerDecision.DatasourceId.Value, plannerDecision.Parameters ?? new Dictionary<string, object?>(), 1, 200),
                        cancellationToken);

                    generatedQuery = null;
                }
            }
            else
            {
                executionResult = await _datasourceService.RunDatasourceAsync(
                    userId,
                    new RunDatasourceRequest(plannerDecision.DatasourceId.Value, plannerDecision.Parameters ?? new Dictionary<string, object?>(), 1, 200),
                    cancellationToken);
            }

            if (executionResult.Rows.Count == 0)
            {
                await EmitFixedReplyAsync(session, actor, NoDataMessage, onEvent, cancellationToken);
                return;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                plannerModel = runtimeSettings.PlannerModel,
                queryPlannerModel = runtimeSettings.PlannerModel,
                responderModel = runtimeSettings.ResponderModel,
                datasourceId = selectedDatasource.Id,
                datasourceCode = selectedDatasource.Code,
                datasourceName = selectedDatasource.Name,
                datasourcePurpose = selectedDatasource.Description,
                datasourceParameters = plannerDecision.Parameters,
                queryPlanReason = queryPlan.Reason,
                queryConfidence = queryPlan.Confidence,
                generatedSql = generatedQuery?.Sql,
                generatedSqlParameters = generatedQuery?.Parameters
            }, JsonOptions);

            var assistantText = await StreamResponderReplyAsync(
                runtimeSettings,
                recentMessages,
                request.Message.Trim(),
                selectedDatasource,
                executionResult,
                onEvent,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(assistantText))
            {
                assistantText = NoDataMessage;
                await onEvent(new AiChatStreamEventDto("delta", assistantText), cancellationToken);
            }

            await AppendMessageAsync(session.Id, "assistant", assistantText, actor, metadata, cancellationToken);
            await onEvent(new AiChatStreamEventDto("done", SessionId: session.Id, SessionTitle: session.Title), cancellationToken);
                    var traceDto = new AiChatTraceDto(
                        DatasourceName: selectedDatasource.Name,
                        DatasourceCode: selectedDatasource.Code,
                        DatasourcePurpose: selectedDatasource.Description,
                        PlannerReason: plannerDecision.Reason,
                        QueryPlanReason: queryPlan.Reason,
                        QueryConfidence: queryPlan.Confidence,
                        GeneratedSql: generatedQuery?.Sql,
                        UsedAgentQuery: generatedQuery is not null,
                        PlannerModel: runtimeSettings.PlannerModel,
                        ResponderModel: runtimeSettings.ResponderModel);
                    await onEvent(new AiChatStreamEventDto("trace", Trace: traceDto), cancellationToken);
        }
        catch (Exception ex)
        {
            await onEvent(new AiChatStreamEventDto("error", $"Chat request failed: {ex.Message}"), cancellationToken);
        }
    }

    private async Task<AiChatSession> GetOrCreateSessionAsync(long userId, string actor, AiChatStreamRequest request, CancellationToken cancellationToken)
    {
        AiChatSession session;
        if (request.SessionId.HasValue)
        {
            session = await _dbContext.AiChatSessions
                .FirstOrDefaultAsync(x => x.Id == request.SessionId.Value && x.UserId == userId && x.IsActive && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Chat session not found.");

            session.ModifiedAt = DateTime.UtcNow;
            session.ModifiedBy = actor;
        }
        else
        {
            session = new AiChatSession
            {
                UserId = userId,
                Title = BuildSessionTitle(request.Message),
                CreatedBy = actor,
                ModifiedBy = actor,
                ModifiedAt = DateTime.UtcNow
            };

            _dbContext.AiChatSessions.Add(session);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task AppendMessageAsync(long sessionId, string role, string content, string actor, string? metadataJson, CancellationToken cancellationToken)
    {
        _dbContext.AiChatMessages.Add(new AiChatMessage
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            MetadataJson = metadataJson,
            CreatedBy = actor,
            ModifiedBy = actor,
            ModifiedAt = DateTime.UtcNow
        });

        var session = await _dbContext.AiChatSessions.FirstAsync(x => x.Id == sessionId, cancellationToken);
        session.ModifiedAt = DateTime.UtcNow;
        session.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EmitFixedReplyAsync(
        AiChatSession session,
        string actor,
        string message,
        Func<AiChatStreamEventDto, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken)
    {
        await onEvent(new AiChatStreamEventDto("delta", message), cancellationToken);
        await AppendMessageAsync(session.Id, "assistant", message, actor, null, cancellationToken);
        await onEvent(new AiChatStreamEventDto("done", SessionId: session.Id, SessionTitle: session.Title), cancellationToken);
    }

    private async Task<PlannerDecision> CreatePlannerDecisionAsync(
        RuntimeAiSettings runtimeSettings,
        IReadOnlyCollection<AiChatMessage> history,
        IReadOnlyCollection<DatasourceDto> datasources,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var datasourceSummary = datasources.Select(x => new
        {
            x.Id,
            x.Name,
            x.Code,
            PurposeDescription = string.IsNullOrWhiteSpace(x.Description)
                ? "No description provided."
                : x.Description,
            AllowedColumns = x.AllowedColumns.Select(c => c.ColumnName),
            parameters = x.Parameters.Select(p => new { p.Name, p.Label, p.DataType, p.IsRequired, p.DefaultValue })
        });

        var prompt = new StringBuilder();
        prompt.AppendLine("You are a planning model for an enterprise reporting assistant.");
        prompt.AppendLine("Choose the single best datasource that can answer the user's question using only the provided accessible datasources.");
        prompt.AppendLine("Each datasource includes a PurposeDescription that explains what business problem it is for. Treat this as the primary signal when selecting datasourceId.");
        prompt.AppendLine("If the question does not match any datasource PurposeDescription, return datasourceId as null.");
        prompt.AppendLine("Return JSON only with keys datasourceId, parameters, reason. If no datasource can answer, set datasourceId to null.");
        prompt.AppendLine("Prefer a null datasourceId over guessing.");
        prompt.AppendLine();
        prompt.AppendLine("Recent conversation:");
        prompt.AppendLine(JsonSerializer.Serialize(history.Select(x => new { x.Role, x.Content }), JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("Accessible datasources:");
        prompt.AppendLine(JsonSerializer.Serialize(datasourceSummary, JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("User question:");
        prompt.AppendLine(userMessage);

        var plannerResponse = await ExecuteChatCompletionAsync(
            runtimeSettings.ApiKey!,
            runtimeSettings.PlannerModel,
            [
                new ChatMessage("system", "Plan datasource selection carefully and output strict JSON only."),
                new ChatMessage("user", prompt.ToString())
            ],
            cancellationToken);

        var plannerJson = ExtractJsonObject(plannerResponse);
        if (string.IsNullOrWhiteSpace(plannerJson))
        {
            return new PlannerDecision(null, null, "No valid planner output.");
        }

        try
        {
            using var doc = JsonDocument.Parse(plannerJson);
            long? datasourceId = null;
            if (doc.RootElement.TryGetProperty("datasourceId", out var datasourceIdElement) && datasourceIdElement.ValueKind is JsonValueKind.Number)
            {
                datasourceId = datasourceIdElement.GetInt64();
            }

            Dictionary<string, object?>? parameters = null;
            if (doc.RootElement.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Object)
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersElement.GetRawText(), JsonOptions);
            }

            var reason = doc.RootElement.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;

            var validDatasourceId = datasourceId.HasValue && datasources.Any(x => x.Id == datasourceId.Value)
                ? datasourceId
                : null;

            return new PlannerDecision(validDatasourceId, parameters, reason);
        }
        catch
        {
            return new PlannerDecision(null, null, "Planner output could not be parsed.");
        }
    }

    private async Task<QueryPlannerPlan> CreateQueryPlannerPlanAsync(
        RuntimeAiSettings runtimeSettings,
        IReadOnlyCollection<AiChatMessage> history,
        DatasourceDto datasource,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var allowedColumns = datasource.AllowedColumns
            .Where(c => c.IsAllowed)
            .Select(c => new { c.ColumnName, c.DataType })
            .ToList();

        var prompt = new StringBuilder();
        prompt.AppendLine("You are a deep-think query planning model for analytics over one selected datasource.");
        prompt.AppendLine("Build an efficient query plan to answer the user question using filters, grouping, sorting, aggregations, and derived calculations when needed.");
        prompt.AppendLine("Think broadly about business semantics in available columns (for example revenue, cost, expense, purchase, sales, tax, discount, margin, profit, loss) and compose accurate calculations from available fields.");
        prompt.AppendLine("When a metric is derived (for example profit/loss), prefer multiple aggregation aliases plus computedExpressions that reference those aliases.");
        prompt.AppendLine("Prioritize correctness over guesswork. If data coverage is partial, return best-effort plan with lower confidence.");
        prompt.AppendLine("Use only the provided allowed columns. If a needed field is missing, leave it out and still return best-effort plan.");
        prompt.AppendLine("Return strict JSON only with keys:");
        prompt.AppendLine("mode, selectColumns, filters, groupBy, aggregations, computedExpressions, sort, limit, reason, confidence");
        prompt.AppendLine("Allowed filter operators: =, !=, >, >=, <, <=, contains, startsWith, endsWith.");
        prompt.AppendLine("Allowed aggregation functions: sum, avg, count, min, max.");
        prompt.AppendLine("computedExpressions must use only +, -, *, /, parentheses, numeric literals, and aggregation aliases.");
        prompt.AppendLine("confidence must be between 0 and 1.");
        prompt.AppendLine();
        prompt.AppendLine("Datasource purpose description:");
        prompt.AppendLine(string.IsNullOrWhiteSpace(datasource.Description) ? "No description provided." : datasource.Description);
        prompt.AppendLine();
        prompt.AppendLine("Datasource allowed columns:");
        prompt.AppendLine(JsonSerializer.Serialize(allowedColumns, JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("Recent conversation:");
        prompt.AppendLine(JsonSerializer.Serialize(history.Select(x => new { x.Role, x.Content }), JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("Current user question:");
        prompt.AppendLine(userMessage);

        var plannerResponse = await ExecuteChatCompletionAsync(
            runtimeSettings.ApiKey!,
            runtimeSettings.PlannerModel,
            [
                new ChatMessage("system", "Generate strict JSON query plan only. No prose."),
                new ChatMessage("user", prompt.ToString())
            ],
            cancellationToken);

        var plannerJson = ExtractJsonObject(plannerResponse);
        if (string.IsNullOrWhiteSpace(plannerJson))
        {
            return new QueryPlannerPlan("detail", [], [], [], [], [], [], 200, "No valid query planner output.", 0.15m);
        }

        try
        {
            using var doc = JsonDocument.Parse(plannerJson);
            var root = doc.RootElement;

            var mode = root.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.String
                ? (modeElement.GetString() ?? "detail")
                : "detail";

            var selectColumns = ReadStringArray(root, "selectColumns");
            var groupBy = ReadStringArray(root, "groupBy");

            var filters = new List<QueryPlannerFilter>();
            if (root.TryGetProperty("filters", out var filtersElement) && filtersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filtersElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("column", out var columnElement) || columnElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var column = columnElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(column))
                    {
                        continue;
                    }

                    var op = item.TryGetProperty("operator", out var opElement) && opElement.ValueKind == JsonValueKind.String
                        ? (opElement.GetString() ?? "=")
                        : "=";

                    string? value = null;
                    if (item.TryGetProperty("value", out var valueElement))
                    {
                        value = valueElement.ValueKind == JsonValueKind.String
                            ? valueElement.GetString()
                            : valueElement.GetRawText();
                    }

                    filters.Add(new QueryPlannerFilter(column, op, value));
                }
            }

            var aggregations = new List<QueryPlannerAggregation>();
            if (root.TryGetProperty("aggregations", out var aggsElement) && aggsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in aggsElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("function", out var fnElement) || fnElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var function = fnElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(function))
                    {
                        continue;
                    }

                    var column = item.TryGetProperty("column", out var colElement) && colElement.ValueKind == JsonValueKind.String
                        ? (colElement.GetString() ?? string.Empty)
                        : string.Empty;

                    var alias = item.TryGetProperty("alias", out var aliasElement) && aliasElement.ValueKind == JsonValueKind.String
                        ? (aliasElement.GetString() ?? string.Empty)
                        : string.Empty;

                    aggregations.Add(new QueryPlannerAggregation(function, column, alias));
                }
            }

            var computedExpressions = new List<QueryPlannerComputedExpression>();
            if (root.TryGetProperty("computedExpressions", out var computedElement) && computedElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in computedElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("alias", out var aliasElement) || aliasElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("expression", out var expressionElement) || expressionElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var alias = aliasElement.GetString() ?? string.Empty;
                    var expression = expressionElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(expression))
                    {
                        continue;
                    }

                    computedExpressions.Add(new QueryPlannerComputedExpression(alias, expression));
                }
            }

            var sort = new List<QueryPlannerSort>();
            if (root.TryGetProperty("sort", out var sortElement) && sortElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in sortElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("column", out var colElement) || colElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var column = colElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(column))
                    {
                        continue;
                    }

                    var direction = item.TryGetProperty("direction", out var dirElement) && dirElement.ValueKind == JsonValueKind.String
                        ? (dirElement.GetString() ?? "ASC")
                        : "ASC";

                    sort.Add(new QueryPlannerSort(column, direction));
                }
            }

            int? limit = null;
            if (root.TryGetProperty("limit", out var limitElement) && limitElement.ValueKind == JsonValueKind.Number)
            {
                limit = limitElement.GetInt32();
            }

            var confidence = 0.6m;
            if (root.TryGetProperty("confidence", out var confidenceElement))
            {
                if (confidenceElement.ValueKind == JsonValueKind.Number && confidenceElement.TryGetDecimal(out var confidenceValue))
                {
                    confidence = confidenceValue;
                }
                else if (confidenceElement.ValueKind == JsonValueKind.String && decimal.TryParse(confidenceElement.GetString(), out confidenceValue))
                {
                    confidence = confidenceValue;
                }
            }

            confidence = Math.Clamp(confidence, 0.0m, 1.0m);

            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;

            return new QueryPlannerPlan(mode, selectColumns, filters, groupBy, aggregations, computedExpressions, sort, limit, reason, confidence);
        }
        catch
        {
            return new QueryPlannerPlan("detail", [], [], [], [], [], [], 200, "Query planner output could not be parsed.", 0.15m);
        }
    }

    private static QueryBuildResult? BuildAgentSelectQuery(DatasourceDto datasource, QueryPlannerPlan plan)
    {
        if (datasource.DatasourceType == Domain.Enums.DatasourceType.StoredProcedure)
        {
            return null;
        }

        var allowed = datasource.AllowedColumns
            .Where(c => c.IsAllowed)
            .ToDictionary(c => c.ColumnName, c => c.DataType, StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
        {
            return null;
        }

        static string EscapeIdentifier(string raw) => raw.Replace("]", "]]", StringComparison.Ordinal);
        string? QuoteColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column) || !allowed.ContainsKey(column))
            {
                return null;
            }

            return $"[src].[{EscapeIdentifier(column)}]";
        }

        var take = Math.Clamp(plan.Limit ?? 200, 1, 500);
        var hasAggregations = plan.Aggregations.Count > 0;
        var hasGrouping = plan.GroupBy.Count > 0;
        var shouldUseAggregateMode = hasAggregations || hasGrouping || string.Equals(plan.Mode, "aggregate", StringComparison.OrdinalIgnoreCase);

        var selectParts = new List<string>();
        var groupByParts = new List<string>();
        var groupAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aggregateAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (shouldUseAggregateMode)
        {
            foreach (var groupCol in plan.GroupBy.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var quoted = QuoteColumn(groupCol);
                if (quoted is null)
                {
                    continue;
                }

                selectParts.Add($"{quoted} AS [{EscapeIdentifier(groupCol)}]");
                groupByParts.Add(quoted);
                groupAliases.Add(groupCol);
            }

            var aggIndex = 0;
            foreach (var agg in plan.Aggregations)
            {
                var fn = agg.Function.Trim().ToUpperInvariant();
                if (fn is not ("SUM" or "AVG" or "COUNT" or "MIN" or "MAX"))
                {
                    continue;
                }

                string expr;
                if (fn == "COUNT" && string.IsNullOrWhiteSpace(agg.Column))
                {
                    expr = "COUNT(1)";
                }
                else
                {
                    var quoted = QuoteColumn(agg.Column);
                    if (quoted is null)
                    {
                        continue;
                    }

                    expr = $"{fn}({quoted})";
                }

                var alias = string.IsNullOrWhiteSpace(agg.Alias) ? $"agg_{aggIndex++}" : agg.Alias.Trim();
                if (string.IsNullOrWhiteSpace(alias) || !AliasRegex.IsMatch(alias))
                {
                    continue;
                }

                aggregateAliases.Add(alias);
                selectParts.Add($"{expr} AS [{EscapeIdentifier(alias)}]");
            }
        }
        else
        {
            var selectedColumns = plan.SelectColumns
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(QuoteColumn)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Cast<string>()
                .ToList();

            if (selectedColumns.Count == 0)
            {
                selectedColumns = allowed.Keys
                    .Take(10)
                    .Select(QuoteColumn)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Cast<string>()
                    .ToList();
            }

            selectParts.AddRange(selectedColumns);
        }

        if (selectParts.Count == 0)
        {
            return null;
        }

        var whereParts = new List<string>();
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var parameterIndex = 0;

        foreach (var filter in plan.Filters)
        {
            var quoted = QuoteColumn(filter.Column);
            if (quoted is null || string.IsNullOrWhiteSpace(filter.Value))
            {
                continue;
            }

            var key = $"p{parameterIndex++}";
            var op = filter.Operator.Trim().ToLowerInvariant();
            switch (op)
            {
                case "=":
                case "!=":
                case ">":
                case ">=":
                case "<":
                case "<=":
                    whereParts.Add($"{quoted} {filter.Operator} @{key}");
                    parameters[key] = filter.Value;
                    break;
                case "contains":
                    whereParts.Add($"{quoted} LIKE @{key}");
                    parameters[key] = $"%{filter.Value}%";
                    break;
                case "startswith":
                    whereParts.Add($"{quoted} LIKE @{key}");
                    parameters[key] = $"{filter.Value}%";
                    break;
                case "endswith":
                    whereParts.Add($"{quoted} LIKE @{key}");
                    parameters[key] = $"%{filter.Value}";
                    break;
            }
        }

        var fromClause = new StringBuilder();
        fromClause.Append("FROM src");

        if (whereParts.Count > 0)
        {
            fromClause.Append(" WHERE ");
            fromClause.Append(string.Join(" AND ", whereParts));
        }

        if (groupByParts.Count > 0)
        {
            fromClause.Append(" GROUP BY ");
            fromClause.Append(string.Join(", ", groupByParts));
        }

        var sortableAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in groupAliases)
        {
            sortableAliases.Add(alias);
        }

        foreach (var alias in aggregateAliases)
        {
            sortableAliases.Add(alias);
        }

        var sql = new StringBuilder();
        var computedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (shouldUseAggregateMode && plan.ComputedExpressions.Count > 0)
        {
            var allowedExpressionAliases = new HashSet<string>(sortableAliases, StringComparer.OrdinalIgnoreCase);
            var computedParts = new List<string>();

            foreach (var computed in plan.ComputedExpressions)
            {
                var alias = computed.Alias.Trim();
                if (string.IsNullOrWhiteSpace(alias) || !AliasRegex.IsMatch(alias) || allowedExpressionAliases.Contains(alias))
                {
                    continue;
                }

                var expressionSql = TryBuildComputedExpressionSql(computed.Expression, allowedExpressionAliases, EscapeIdentifier);
                if (string.IsNullOrWhiteSpace(expressionSql))
                {
                    continue;
                }

                computedParts.Add($"{expressionSql} AS [{EscapeIdentifier(alias)}]");
                computedAliases.Add(alias);
                allowedExpressionAliases.Add(alias);
            }

            if (computedParts.Count > 0)
            {
                sql.Append("SELECT TOP (@__take) q.*, ");
                sql.Append(string.Join(", ", computedParts));
                sql.Append(" FROM (");
                sql.Append("SELECT ");
                sql.Append(string.Join(", ", selectParts));
                sql.Append(' ');
                sql.Append(fromClause);
                sql.Append(") AS q");

                foreach (var alias in computedAliases)
                {
                    sortableAliases.Add(alias);
                }
            }
        }

        if (sql.Length == 0)
        {
            sql.Append("SELECT TOP (@__take) ");
            sql.Append(string.Join(", ", selectParts));
            sql.Append(' ');
            sql.Append(fromClause);
        }

        var orderByParts = new List<string>();
        foreach (var sort in plan.Sort)
        {
            var direction = string.Equals(sort.Direction, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            var quotedColumn = QuoteColumn(sort.Column);
            if (quotedColumn is not null)
            {
                orderByParts.Add($"{quotedColumn} {direction}");
                continue;
            }

            if (sortableAliases.Contains(sort.Column))
            {
                orderByParts.Add($"[{EscapeIdentifier(sort.Column)}] {direction}");
            }
        }

        if (orderByParts.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", orderByParts));
        }

        return new QueryBuildResult(sql.ToString(), parameters, take);
    }

    private static string? TryBuildComputedExpressionSql(string expression, IReadOnlySet<string> allowedAliases, Func<string, string> escapeIdentifier)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 160)
        {
            return null;
        }

        if (UnsupportedExpressionCharactersRegex.IsMatch(expression))
        {
            return null;
        }

        var compactSource = Regex.Replace(expression, "\\s+", string.Empty);
        var matches = ComputedExpressionTokenRegex.Matches(expression);
        if (matches.Count == 0)
        {
            return null;
        }

        var compactTokens = string.Concat(matches.Select(match => match.Value));
        if (!string.Equals(compactSource, compactTokens, StringComparison.Ordinal))
        {
            return null;
        }

        var sqlBuilder = new StringBuilder();
        foreach (Match match in matches)
        {
            var token = match.Value;
            if (IdentifierTokenRegex.IsMatch(token))
            {
                if (!allowedAliases.Contains(token))
                {
                    return null;
                }

                sqlBuilder.Append($"[{escapeIdentifier(token)}]");
                continue;
            }

            sqlBuilder.Append(token);
        }

        return sqlBuilder.ToString();
    }

    private static IReadOnlyCollection<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }
        }

        return values;
    }

    private async Task<string> StreamResponderReplyAsync(
        RuntimeAiSettings runtimeSettings,
        IReadOnlyCollection<AiChatMessage> history,
        string userMessage,
        DatasourceDto? datasource,
        DatasourceExecutionResult executionResult,
        Func<AiChatStreamEventDto, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken)
    {
        var sampleRows = executionResult.Rows.Take(60).ToList();
        var prompt = new StringBuilder();
        prompt.AppendLine("You are Nexa, a modern analytics assistant inside an enterprise reporting product.");
        prompt.AppendLine("Answer using only the provided datasource result.");
        prompt.AppendLine("When rows are present, provide the best possible answer from those rows and avoid claiming data is unavailable.");
        prompt.AppendLine("If period-specific filtering (for example month/year) is not visible in the returned rows, state that this answer is based on the returned query result.");
        prompt.AppendLine("Always answer in English only. Do not use any other language.");
        prompt.AppendLine("Be concise, direct, and business-friendly.");
        prompt.AppendLine();
        prompt.AppendLine($"Datasource: {datasource?.Name} ({datasource?.Code})");
        prompt.AppendLine($"Columns: {string.Join(", ", executionResult.Columns)}");
        prompt.AppendLine($"Returned rows: {executionResult.TotalCount}");
        prompt.AppendLine("Sample rows JSON:");
        prompt.AppendLine(JsonSerializer.Serialize(sampleRows, JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("Recent conversation:");
        prompt.AppendLine(JsonSerializer.Serialize(history.Select(x => new { x.Role, x.Content }), JsonOptions));
        prompt.AppendLine();
        prompt.AppendLine("Current user question:");
        prompt.AppendLine(userMessage);

        var httpClient = _httpClientFactory.CreateClient("OpenAI");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", runtimeSettings.ApiKey);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var payload = new
        {
            model = runtimeSettings.ResponderModel,
            temperature = 0.2,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = "You answer strictly from datasource evidence. Always reply in English only." },
                new { role = "user", content = prompt.ToString() }
            }
        };

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using var reader = new StreamReader(responseBody);
            var errorBody = await reader.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI request failed: {response.StatusCode} {errorBody}");
        }

        var assistantText = new StringBuilder();
        using var streamReader = new StreamReader(responseBody);
        while (!streamReader.EndOfStream)
        {
            var line = await streamReader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var payloadLine = line[6..].Trim();
            if (payloadLine == "[DONE]")
            {
                break;
            }

            var delta = TryExtractStreamDelta(payloadLine);
            if (string.IsNullOrWhiteSpace(delta))
            {
                continue;
            }

            assistantText.Append(delta);
            await onEvent(new AiChatStreamEventDto("delta", delta), cancellationToken);
        }

        return assistantText.ToString().Trim();
    }

    private async Task<string> ExecuteChatCompletionAsync(
        string apiKey,
        string model,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("OpenAI");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            temperature = 0.1,
            messages = messages.Select(x => new { role = x.Role, content = x.Content })
        };

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI request failed: {response.StatusCode} {responseText}");
        }

        using var doc = JsonDocument.Parse(responseText);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var messageElement = choices[0].GetProperty("message");
        if (messageElement.TryGetProperty("content", out var contentElement))
        {
            return ReadContentValue(contentElement);
        }

        return string.Empty;
    }

    private async Task<RuntimeAiSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SystemSettings
            .Where(x => x.Category == AiChatCategory && x.IsActive && !x.IsDeleted)
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);

        settings.TryGetValue("ApiKey", out var storedApiKey);
        settings.TryGetValue("PlannerModel", out var plannerModel);
        settings.TryGetValue("ResponderModel", out var responderModel);

        return new RuntimeAiSettings(
            _settingsSecretProtectionService.Unprotect(storedApiKey),
            string.IsNullOrWhiteSpace(plannerModel) ? "gpt-5.4" : plannerModel,
            string.IsNullOrWhiteSpace(responderModel) ? "gpt-5.4-mini" : responderModel);
    }

    private static string BuildSessionTitle(string message)
    {
        var normalized = string.Join(' ', message.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= 48)
        {
            return normalized;
        }

        return normalized[..48].TrimEnd() + "...";
    }

    private static string BuildPreview(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "New chat";
        }

        var normalized = string.Join(' ', message.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 72 ? normalized : normalized[..72].TrimEnd() + "...";
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return raw[start..(end + 1)];
    }

    private static string ReadContentValue(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? string.Empty;
        }

        if (contentElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in contentElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Append(item.GetString());
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(textElement.GetString());
                    continue;
                }

                if (item.TryGetProperty("content", out var nestedContentElement) && nestedContentElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(nestedContentElement.GetString());
                }
            }
        }

        return builder.ToString();
    }

    private static string? TryExtractStreamDelta(string payloadLine)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadLine);
            var choice = doc.RootElement.GetProperty("choices")[0];
            if (!choice.TryGetProperty("delta", out var deltaElement))
            {
                return null;
            }

            if (!deltaElement.TryGetProperty("content", out var contentElement))
            {
                return null;
            }

            return ReadContentValue(contentElement);
        }
        catch
        {
            return null;
        }
    }

    private sealed record RuntimeAiSettings(string? ApiKey, string PlannerModel, string ResponderModel);
    private sealed record PlannerDecision(long? DatasourceId, Dictionary<string, object?>? Parameters, string Reason);
    private sealed record QueryPlannerPlan(
        string Mode,
        IReadOnlyCollection<string> SelectColumns,
        IReadOnlyCollection<QueryPlannerFilter> Filters,
        IReadOnlyCollection<string> GroupBy,
        IReadOnlyCollection<QueryPlannerAggregation> Aggregations,
        IReadOnlyCollection<QueryPlannerComputedExpression> ComputedExpressions,
        IReadOnlyCollection<QueryPlannerSort> Sort,
        int? Limit,
        string Reason,
        decimal Confidence);
    private sealed record QueryPlannerFilter(string Column, string Operator, string? Value);
    private sealed record QueryPlannerAggregation(string Function, string Column, string Alias);
    private sealed record QueryPlannerComputedExpression(string Alias, string Expression);
    private sealed record QueryPlannerSort(string Column, string Direction);
    private sealed record QueryBuildResult(string Sql, Dictionary<string, object?> Parameters, int PageSize);
    private sealed record ChatMessage(string Role, string Content);
}