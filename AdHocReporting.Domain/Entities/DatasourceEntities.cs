using AdHocReporting.Domain.Common;
using AdHocReporting.Domain.Enums;

namespace AdHocReporting.Domain.Entities;

public sealed class Datasource : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DatasourceType DatasourceType { get; set; }
    public string SqlDefinitionOrObjectName { get; set; } = string.Empty;
    public string? ConnectionName { get; set; }

    public ICollection<DatasourceParameter> Parameters { get; set; } = new List<DatasourceParameter>();
    public ICollection<DatasourceColumnMetadata> AllowedColumns { get; set; } = new List<DatasourceColumnMetadata>();
    public ICollection<DatasourceRoleAccess> RoleAccesses { get; set; } = new List<DatasourceRoleAccess>();
    public ICollection<DatasourceUserAccess> UserAccesses { get; set; } = new List<DatasourceUserAccess>();
}

public sealed class DatasourceParameter : AuditableEntity
{
    public long DatasourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionsJson { get; set; }

    public Datasource? Datasource { get; set; }
}

public sealed class DatasourceColumnMetadata : AuditableEntity
{
    public long DatasourceId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsAllowed { get; set; } = true;

    public Datasource? Datasource { get; set; }
}

public sealed class DatasourceRoleAccess : AuditableEntity
{
    public long DatasourceId { get; set; }
    public long RoleId { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanUse { get; set; } = true;
    public bool CanManage { get; set; }

    public Datasource? Datasource { get; set; }
    public Role? Role { get; set; }
}

public sealed class DatasourceUserAccess : AuditableEntity
{
    public long DatasourceId { get; set; }
    public long UserId { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanUse { get; set; } = true;
    public bool CanManage { get; set; }

    public Datasource? Datasource { get; set; }
    public User? User { get; set; }
}
