using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdHocReporting.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdHocReporting.Infrastructure.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    // SQL Server error numbers for unique constraint / primary key violations
    private const int SqlUniqueConstraintError = 2627;
    private const int SqlUniqueIndexError = 2601;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

            int statusCode;
            string userMessage;

            if (ex is DbUpdateException dbEx && IsUniqueConstraintViolation(dbEx, out var duplicateValue))
            {
                statusCode = (int)HttpStatusCode.BadRequest;
                userMessage = string.IsNullOrWhiteSpace(duplicateValue)
                    ? "A record with the same unique value already exists."
                    : $"'{duplicateValue}' already exists. Please use a different value.";
            }
            else
            {
                statusCode = ex switch
                {
                    UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                    InvalidOperationException => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                };
                userMessage = ex.Message;
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<string>.Fail("Request failed", new[] { userMessage });
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex, out string duplicateValue)
    {
        duplicateValue = string.Empty;

        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
            (sqlEx.Number == SqlUniqueConstraintError || sqlEx.Number == SqlUniqueIndexError))
        {
            // Extract the duplicate value from the SQL message, e.g.:
            // "... The duplicate key value is (RPT_BRANCH_SALES)."
            var msg = sqlEx.Message;
            var start = msg.IndexOf('(');
            var end = msg.IndexOf(')', start + 1);
            if (start >= 0 && end > start)
                duplicateValue = msg.Substring(start + 1, end - start - 1);

            return true;
        }

        return false;
    }
}
