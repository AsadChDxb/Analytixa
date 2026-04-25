using System.Text.Json;
using AdHocReporting.API.Extensions;
using AdHocReporting.Application.DTOs.AI;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-chat")]
public sealed class AiChatController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAiChatService _aiChatService;

    public AiChatController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyCollection<AiChatSessionSummaryDto>>> GetSessions([FromQuery] int take = 12, CancellationToken cancellationToken = default)
    {
        var result = await _aiChatService.GetSessionsAsync(User.GetUserId(), take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:long}")]
    public async Task<ActionResult<AiChatSessionDetailDto>> GetSession(long sessionId, CancellationToken cancellationToken)
    {
        var result = await _aiChatService.GetSessionAsync(User.GetUserId(), sessionId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("sessions/{sessionId:long}")]
    public async Task<ActionResult<string>> DeleteSession(long sessionId, CancellationToken cancellationToken)
    {
        await _aiChatService.DeleteSessionAsync(User.GetUserId(), sessionId, User.GetUsername(), cancellationToken);
        return Ok("Session deleted.");
    }

    [HttpPost("stream")]
    public async Task<IActionResult> Stream([FromBody] AiChatStreamRequest request, CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";
        Response.Headers.Append("Cache-Control", "no-cache");

        async Task WriteEventAsync(AiChatStreamEventDto eventDto, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(eventDto, JsonOptions);
            await Response.WriteAsync(json + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await _aiChatService.StreamReplyAsync(User.GetUserId(), User.GetUsername(), request, WriteEventAsync, cancellationToken);
        return new EmptyResult();
    }
}