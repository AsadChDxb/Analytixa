using AdHocReporting.Domain.Entities;
using AdHocReporting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Persistence;

public sealed class AdHocDbContext : DbContext
{
    public AdHocDbContext(DbContextOptions<AdHocDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Datasource> Datasources => Set<Datasource>();
    public DbSet<DatasourceParameter> DatasourceParameters => Set<DatasourceParameter>();
    public DbSet<DatasourceColumnMetadata> DatasourceColumnsMetadata => Set<DatasourceColumnMetadata>();
    public DbSet<DatasourceRoleAccess> DatasourceRoleAccess => Set<DatasourceRoleAccess>();
    public DbSet<DatasourceUserAccess> DatasourceUserAccess => Set<DatasourceUserAccess>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportColumn> ReportColumns => Set<ReportColumn>();
    public DbSet<ReportFilter> ReportFilters => Set<ReportFilter>();
    public DbSet<ReportSort> ReportSorts => Set<ReportSort>();
    public DbSet<ReportGroup> ReportGroups => Set<ReportGroup>();
    public DbSet<ReportAggregation> ReportAggregations => Set<ReportAggregation>();
    public DbSet<ReportParameter> ReportParameters => Set<ReportParameter>();
    public DbSet<ReportRoleAccess> ReportRoleAccess => Set<ReportRoleAccess>();
    public DbSet<ReportUserAccess> ReportUserAccess => Set<ReportUserAccess>();
    public DbSet<ReportBranding> ReportBrandings => Set<ReportBranding>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Datasource>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Report>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Dashboard>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SystemSetting>().HasIndex(x => new { x.Category, x.SettingKey }).IsUnique();
        modelBuilder.Entity<AiChatSession>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<AiChatMessage>().HasIndex(x => new { x.SessionId, x.CreatedAt });

        modelBuilder.Entity<UserRole>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        modelBuilder.Entity<DatasourceRoleAccess>().HasIndex(x => new { x.DatasourceId, x.RoleId }).IsUnique();
        modelBuilder.Entity<DatasourceUserAccess>().HasIndex(x => new { x.DatasourceId, x.UserId }).IsUnique();
        modelBuilder.Entity<ReportRoleAccess>().HasIndex(x => new { x.ReportId, x.RoleId }).IsUnique();
        modelBuilder.Entity<ReportUserAccess>().HasIndex(x => new { x.ReportId, x.UserId }).IsUnique();
        modelBuilder.Entity<ReportBranding>().ToTable("ReportBranding");
        modelBuilder.Entity<AiChatSession>()
            .HasMany(x => x.Messages)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        Seed(modelBuilder);
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        var createdAt = DateTime.UtcNow;
        const string actor = "seed";

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Administrator", Code = "Admin", Description = "System administrator", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new Role { Id = 2, Name = "IT", Code = "IT", Description = "Technical admin", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new Role { Id = 3, Name = "Report User", Code = "ReportUser", Description = "Standard report user", CreatedAt = createdAt, CreatedBy = actor, IsActive = true }
        );

        var permissionData = new List<Permission>();
        var codes = new[]
        {
            PermissionCodes.ManageUsers,
            PermissionCodes.ManageRoles,
            PermissionCodes.ManagePermissions,
            PermissionCodes.ManageDatasource,
            PermissionCodes.ViewDatasource,
            PermissionCodes.UseDatasource,
            PermissionCodes.ManageReport,
            PermissionCodes.ViewReport,
            PermissionCodes.RunReport,
            PermissionCodes.ExportReport,
            PermissionCodes.ViewAuditLogs
        };

        for (var i = 0; i < codes.Length; i++)
        {
            permissionData.Add(new Permission
            {
                Id = i + 1,
                Name = codes[i],
                Code = codes[i],
                Description = codes[i],
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            });
        }

        modelBuilder.Entity<Permission>().HasData(permissionData);

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@adhoc.local",
            FullName = "System Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
            MustChangePassword = false,
            CreatedAt = createdAt,
            CreatedBy = actor,
            IsActive = true
        });

        modelBuilder.Entity<UserRole>().HasData(new UserRole
        {
            Id = 1,
            UserId = 1,
            RoleId = 1,
            CreatedAt = createdAt,
            CreatedBy = actor,
            IsActive = true
        });

        var rolePermissions = new List<RolePermission>();
        var rpId = 1L;
        foreach (var permission in permissionData)
        {
            rolePermissions.Add(new RolePermission
            {
                Id = rpId++,
                RoleId = 1,
                PermissionId = permission.Id,
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            });
        }

        foreach (var permission in permissionData.Where(x => x.Code is PermissionCodes.ManageDatasource or PermissionCodes.ViewDatasource or PermissionCodes.UseDatasource or PermissionCodes.ViewReport or PermissionCodes.RunReport or PermissionCodes.ExportReport))
        {
            rolePermissions.Add(new RolePermission
            {
                Id = rpId++,
                RoleId = 2,
                PermissionId = permission.Id,
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            });
        }

        foreach (var permission in permissionData.Where(x => x.Code is PermissionCodes.ViewDatasource or PermissionCodes.UseDatasource or PermissionCodes.ViewReport or PermissionCodes.RunReport or PermissionCodes.ExportReport))
        {
            rolePermissions.Add(new RolePermission
            {
                Id = rpId++,
                RoleId = 3,
                PermissionId = permission.Id,
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            });
        }

        modelBuilder.Entity<RolePermission>().HasData(rolePermissions);

        modelBuilder.Entity<Datasource>().HasData(
            new Datasource
            {
                Id = 1,
                Name = "Employees Listing",
                Code = "DS_EMP_LIST",
                Description = "Employees test datasource",
                DatasourceType = DatasourceType.View,
                SqlDefinitionOrObjectName = "vw_EmployeeList",
                ConnectionName = "DefaultConnection",
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            },
            new Datasource
            {
                Id = 2,
                Name = "Sales by Date",
                Code = "DS_SALES_DATE",
                Description = "Sales procedure datasource",
                DatasourceType = DatasourceType.StoredProcedure,
                SqlDefinitionOrObjectName = "sp_Report_SalesByDate",
                ConnectionName = "DefaultConnection",
                CreatedAt = createdAt,
                CreatedBy = actor,
                IsActive = true
            }
        );

        modelBuilder.Entity<DatasourceRoleAccess>().HasData(
            new DatasourceRoleAccess { Id = 1, DatasourceId = 1, RoleId = 3, CanView = true, CanUse = true, CanManage = false, CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new DatasourceRoleAccess { Id = 2, DatasourceId = 2, RoleId = 2, CanView = true, CanUse = true, CanManage = true, CreatedAt = createdAt, CreatedBy = actor, IsActive = true }
        );

        modelBuilder.Entity<Report>().HasData(new Report
        {
            Id = 1,
            Name = "Employee Summary",
            Code = "RPT_EMP_SUM",
            Description = "Default seeded report",
            DatasourceId = 1,
            OwnerUserId = 1,
            IsPublic = true,
            IsPrivate = false,
            CreatedAt = createdAt,
            CreatedBy = actor,
            IsActive = true
        });

        modelBuilder.Entity<ReportBranding>().HasData(new ReportBranding
        {
            Id = 1,
            ReportId = 1,
            Title = "Employee Summary",
            Subtitle = "Default report",
            HeaderAlignment = "Left",
            ShowLogo = false,
            ShowGeneratedBy = true,
            ShowGeneratedDate = true,
            CreatedAt = createdAt,
            CreatedBy = actor,
            IsActive = true
        });

        modelBuilder.Entity<Dashboard>().HasData(new Dashboard
        {
            Id = 1,
            Name = "Executive Snapshot",
            Code = "DSH_EXEC_SNAPSHOT",
            Description = "Seeded dashboard with KPI tiles and a chart.",
            DatasourceId = 1,
            OwnerUserId = 1,
            DefinitionJson = "{\"filters\":[],\"widgets\":[{\"id\":\"seed-kpi\",\"type\":\"kpi\",\"title\":\"Employees\",\"layout\":{\"columnStart\":1,\"columnSpan\":4,\"rowSpan\":1,\"minHeight\":180},\"config\":{\"metric\":\"count\",\"label\":\"Total employees\",\"accent\":\"#3aa96b\"}},{\"id\":\"seed-chart\",\"type\":\"bar\",\"title\":\"Employees by Department\",\"layout\":{\"columnStart\":1,\"columnSpan\":8,\"rowSpan\":2,\"minHeight\":320},\"config\":{\"xField\":\"Department\",\"yField\":\"EmployeeId\",\"aggregate\":\"count\",\"showLegend\":false,\"xLabel\":\"Department\",\"yLabel\":\"Employees\",\"accent\":\"#ff7a59\"}}],\"theme\":{\"palette\":[\"#3aa96b\",\"#ff7a59\",\"#4c7fff\",\"#f6bd16\"]}}",
            CreatedAt = createdAt,
            CreatedBy = actor,
            IsActive = true
        });

        modelBuilder.Entity<SystemSetting>().HasData(
            new SystemSetting { Id = 1, Category = "Export", SettingKey = "MaxExportRows", SettingValue = "50000", Description = "Maximum rows allowed in export", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 2, Category = "Report", SettingKey = "MaxPreviewRows", SettingValue = "1000", Description = "Maximum rows allowed in preview", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 3, Category = "Branding", SettingKey = "CompanyName", SettingValue = "Contoso Holdings", Description = "Default company name for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 4, Category = "Branding", SettingKey = "Address", SettingValue = "Main Boulevard, Lahore", Description = "Default company address for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 5, Category = "Branding", SettingKey = "Phone", SettingValue = "+92-300-0000000", Description = "Default phone for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 6, Category = "Branding", SettingKey = "Email", SettingValue = "info@contoso.local", Description = "Default email for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 7, Category = "Branding", SettingKey = "FooterText", SettingValue = "Confidential - Internal Use", Description = "Default footer text for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 8, Category = "Branding", SettingKey = "CompanyLogoDataUrl", SettingValue = string.Empty, Description = "Default logo (data URL) for report branding", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 9, Category = "Datasource", SettingKey = "ExternalConnectionString", SettingValue = string.Empty, Description = "Optional external connection string for datasource/report runtime", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 10, Category = "AiChat", SettingKey = "ApiKey", SettingValue = string.Empty, Description = "Protected OpenAI API key for Nexa assistant", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 11, Category = "AiChat", SettingKey = "PlannerModel", SettingValue = "gpt-5.4", Description = "Planning model for datasource selection", CreatedAt = createdAt, CreatedBy = actor, IsActive = true },
            new SystemSetting { Id = 12, Category = "AiChat", SettingKey = "ResponderModel", SettingValue = "gpt-5.4-mini", Description = "Streaming responder model for Nexa assistant", CreatedAt = createdAt, CreatedBy = actor, IsActive = true }
        );
    }
}
