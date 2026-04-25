using AdHocReporting.Domain.Common;

namespace AdHocReporting.Domain.Entities;

public sealed class AuditLog : AuditableEntity
{
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? PayloadSummary { get; set; }
    public string? IpAddress { get; set; }

    public User? User { get; set; }
}

public sealed class SystemSetting : AuditableEntity
{
    public string Category { get; set; } = string.Empty;
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class AiChatSession : AuditableEntity
{
    public long UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    public User? User { get; set; }
    public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
}

public sealed class AiChatMessage : AuditableEntity
{
    public long SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }

    public AiChatSession? Session { get; set; }
}
