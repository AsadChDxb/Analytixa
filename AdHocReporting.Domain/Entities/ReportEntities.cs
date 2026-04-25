using AdHocReporting.Domain.Common;

namespace AdHocReporting.Domain.Entities;

public sealed class Report : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public long OwnerUserId { get; set; }
    public bool IsPublic { get; set; }
    public bool IsPrivate { get; set; } = true;

    public Datasource? Datasource { get; set; }
    public User? OwnerUser { get; set; }
    public ICollection<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
    public ICollection<ReportFilter> Filters { get; set; } = new List<ReportFilter>();
    public ICollection<ReportSort> Sorts { get; set; } = new List<ReportSort>();
    public ICollection<ReportGroup> Groups { get; set; } = new List<ReportGroup>();
    public ICollection<ReportAggregation> Aggregations { get; set; } = new List<ReportAggregation>();
    public ICollection<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
    public ICollection<ReportRoleAccess> RoleAccesses { get; set; } = new List<ReportRoleAccess>();
    public ICollection<ReportUserAccess> UserAccesses { get; set; } = new List<ReportUserAccess>();
    public ReportBranding? Branding { get; set; }
}

public sealed class ReportColumn : AuditableEntity
{
    public long ReportId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public Report? Report { get; set; }
}

public sealed class ReportFilter : AuditableEntity
{
    public long ReportId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string";

    public Report? Report { get; set; }
}

public sealed class ReportSort : AuditableEntity
{
    public long ReportId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string Direction { get; set; } = "ASC";
    public int SortOrder { get; set; }

    public Report? Report { get; set; }
}

public sealed class ReportGroup : AuditableEntity
{
    public long ReportId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public int GroupOrder { get; set; }

    public Report? Report { get; set; }
}

public sealed class ReportAggregation : AuditableEntity
{
    public long ReportId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string AggregateFunction { get; set; } = "Count";

    public Report? Report { get; set; }
}

public sealed class ReportParameter : AuditableEntity
{
    public long ReportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string DataType { get; set; } = "string";

    public Report? Report { get; set; }
}

public sealed class ReportRoleAccess : AuditableEntity
{
    public long ReportId { get; set; }
    public long RoleId { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanRun { get; set; } = true;
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanExport { get; set; }

    public Report? Report { get; set; }
    public Role? Role { get; set; }
}

public sealed class ReportUserAccess : AuditableEntity
{
    public long ReportId { get; set; }
    public long UserId { get; set; }
    public bool CanView { get; set; } = true;
    public bool CanRun { get; set; } = true;
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanExport { get; set; }

    public Report? Report { get; set; }
    public User? User { get; set; }
}

public sealed class ReportBranding : AuditableEntity
{
    public long ReportId { get; set; }
    public string? LogoUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? HeaderFieldsJson { get; set; }
    public string HeaderAlignment { get; set; } = "Left";
    public bool ShowLogo { get; set; } = true;
    public bool ShowGeneratedDate { get; set; } = true;
    public bool ShowGeneratedBy { get; set; } = true;
    public string? FooterText { get; set; }
    public string? WatermarkText { get; set; }

    public Report? Report { get; set; }
}
