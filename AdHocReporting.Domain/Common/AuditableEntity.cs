namespace AdHocReporting.Domain.Common;

public abstract class AuditableEntity
{
    public long Id { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
