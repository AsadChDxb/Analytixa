using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;

namespace AdHocReporting.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly AdHocDbContext _dbContext;

    public AuditService(AdHocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(long? userId, string action, string entityName, string? entityId, string? payloadSummary, string? ipAddress, string actor, CancellationToken cancellationToken = default)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            PayloadSummary = payloadSummary,
            IpAddress = ipAddress,
            CreatedBy = actor
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
