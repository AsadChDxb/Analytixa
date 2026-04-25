using AdHocReporting.Domain.Common;

namespace AdHocReporting.Domain.Entities;

public sealed class Dashboard : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public long OwnerUserId { get; set; }
    public string DefinitionJson { get; set; } = "{}";

    public Datasource? Datasource { get; set; }
    public User? OwnerUser { get; set; }
}