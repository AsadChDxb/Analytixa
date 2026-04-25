/*
AdHoc Reporting Module - SQL Server schema
Includes auth, RBAC, datasource/reporting, branding, audit, settings, and dummy data.
*/

IF DB_ID('AdHocReportingDb') IS NULL
BEGIN
    CREATE DATABASE AdHocReportingDb;
END;
GO

USE AdHocReportingDb;
GO

CREATE TABLE dbo.Users (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(200) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    MustChangePassword BIT NOT NULL DEFAULT 0,
    LastLoginAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL
);
GO

CREATE TABLE dbo.Roles (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Code NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL
);
GO

CREATE TABLE dbo.Permissions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Code NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL
);
GO

CREATE TABLE dbo.UserRoles (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    RoleId BIGINT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT UQ_UserRoles_UserRole UNIQUE (UserId, RoleId)
);
GO

CREATE TABLE dbo.RolePermissions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RoleId BIGINT NOT NULL,
    PermissionId BIGINT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id),
    CONSTRAINT UQ_RolePermissions_RolePermission UNIQUE (RoleId, PermissionId)
);
GO

CREATE TABLE dbo.RefreshTokens (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Token NVARCHAR(500) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    RevokedAt DATETIME2 NULL,
    ReplacedByToken NVARCHAR(500) NULL,
    CreatedByIp NVARCHAR(100) NOT NULL,
    RevokedByIp NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.Datasources (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Code NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(1000) NOT NULL,
    DatasourceType TINYINT NOT NULL,
    SqlDefinitionOrObjectName NVARCHAR(MAX) NOT NULL,
    ConnectionName NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL
);
GO

CREATE TABLE dbo.DatasourceParameters (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DatasourceId BIGINT NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Label NVARCHAR(200) NOT NULL,
    DataType NVARCHAR(50) NOT NULL,
    IsRequired BIT NOT NULL DEFAULT 0,
    DefaultValue NVARCHAR(500) NULL,
    OptionsJson NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_DatasourceParameters_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id)
);
GO

CREATE TABLE dbo.DatasourceColumnsMetadata (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DatasourceId BIGINT NOT NULL,
    ColumnName NVARCHAR(200) NOT NULL,
    DataType NVARCHAR(100) NOT NULL,
    IsAllowed BIT NOT NULL DEFAULT 1,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_DatasourceColumnsMetadata_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id)
);
GO

CREATE TABLE dbo.DatasourceRoleAccess (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DatasourceId BIGINT NOT NULL,
    RoleId BIGINT NOT NULL,
    CanView BIT NOT NULL DEFAULT 1,
    CanUse BIT NOT NULL DEFAULT 1,
    CanManage BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_DatasourceRoleAccess_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id),
    CONSTRAINT FK_DatasourceRoleAccess_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT UQ_DatasourceRoleAccess UNIQUE (DatasourceId, RoleId)
);
GO

CREATE TABLE dbo.DatasourceUserAccess (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DatasourceId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    CanView BIT NOT NULL DEFAULT 1,
    CanUse BIT NOT NULL DEFAULT 1,
    CanManage BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_DatasourceUserAccess_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id),
    CONSTRAINT FK_DatasourceUserAccess_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT UQ_DatasourceUserAccess UNIQUE (DatasourceId, UserId)
);
GO

CREATE TABLE dbo.Reports (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Code NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(1000) NOT NULL,
    DatasourceId BIGINT NOT NULL,
    OwnerUserId BIGINT NOT NULL,
    IsPublic BIT NOT NULL DEFAULT 0,
    IsPrivate BIT NOT NULL DEFAULT 1,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_Reports_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id),
    CONSTRAINT FK_Reports_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.ReportColumns (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    ColumnName NVARCHAR(200) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    DisplayOrder INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportColumns_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportFilters (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    FieldName NVARCHAR(200) NOT NULL,
    [Operator] NVARCHAR(20) NOT NULL,
    [Value] NVARCHAR(500) NULL,
    ValueType NVARCHAR(50) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportFilters_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportSorts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    FieldName NVARCHAR(200) NOT NULL,
    Direction NVARCHAR(4) NOT NULL,
    SortOrder INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportSorts_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportGroups (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    FieldName NVARCHAR(200) NOT NULL,
    GroupOrder INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportGroups_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportAggregations (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    FieldName NVARCHAR(200) NOT NULL,
    AggregateFunction NVARCHAR(20) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportAggregations_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportParameters (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    [Value] NVARCHAR(500) NULL,
    DataType NVARCHAR(50) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportParameters_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id)
);
GO

CREATE TABLE dbo.ReportRoleAccess (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    RoleId BIGINT NOT NULL,
    CanView BIT NOT NULL DEFAULT 1,
    CanRun BIT NOT NULL DEFAULT 1,
    CanEdit BIT NOT NULL DEFAULT 0,
    CanDelete BIT NOT NULL DEFAULT 0,
    CanExport BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportRoleAccess_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id),
    CONSTRAINT FK_ReportRoleAccess_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT UQ_ReportRoleAccess UNIQUE (ReportId, RoleId)
);
GO

CREATE TABLE dbo.ReportUserAccess (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    CanView BIT NOT NULL DEFAULT 1,
    CanRun BIT NOT NULL DEFAULT 1,
    CanEdit BIT NOT NULL DEFAULT 0,
    CanDelete BIT NOT NULL DEFAULT 0,
    CanExport BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportUserAccess_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id),
    CONSTRAINT FK_ReportUserAccess_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT UQ_ReportUserAccess UNIQUE (ReportId, UserId)
);
GO

CREATE TABLE dbo.ReportBranding (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReportId BIGINT NOT NULL,
    LogoUrl NVARCHAR(1000) NULL,
    Title NVARCHAR(300) NOT NULL,
    Subtitle NVARCHAR(300) NULL,
    HeaderFieldsJson NVARCHAR(MAX) NULL,
    HeaderAlignment NVARCHAR(20) NOT NULL,
    ShowLogo BIT NOT NULL DEFAULT 1,
    ShowGeneratedDate BIT NOT NULL DEFAULT 1,
    ShowGeneratedBy BIT NOT NULL DEFAULT 1,
    FooterText NVARCHAR(1000) NULL,
    WatermarkText NVARCHAR(300) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportBranding_Reports FOREIGN KEY (ReportId) REFERENCES dbo.Reports(Id),
    CONSTRAINT UQ_ReportBranding_Report UNIQUE (ReportId)
);
GO

CREATE TABLE dbo.AuditLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NULL,
    [Action] NVARCHAR(100) NOT NULL,
    EntityName NVARCHAR(200) NOT NULL,
    EntityId NVARCHAR(100) NULL,
    PayloadSummary NVARCHAR(1000) NULL,
    IpAddress NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.SystemSettings (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL,
    SettingKey NVARCHAR(100) NOT NULL,
    SettingValue NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL,
    ModifiedAt DATETIME2 NULL,
    ModifiedBy NVARCHAR(100) NULL,
    CONSTRAINT UQ_SystemSettings UNIQUE (Category, SettingKey)
);
GO

/* Dummy domain test tables */
CREATE TABLE dbo.DummyEmployees (
    EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeCode NVARCHAR(50) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    Department NVARCHAR(100) NOT NULL,
    Salary DECIMAL(18,2) NOT NULL,
    JoiningDate DATE NOT NULL
);
GO

CREATE TABLE dbo.DummySales (
    SalesId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Branch NVARCHAR(100) NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    SalesDate DATE NOT NULL
);
GO

CREATE VIEW dbo.vw_EmployeeList
AS
SELECT EmployeeId, EmployeeCode, FullName, Department, Salary, JoiningDate
FROM dbo.DummyEmployees;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Report_SalesByDate
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        SalesId,
        Branch,
        ProductName,
        Quantity,
        UnitPrice,
        (Quantity * UnitPrice) AS TotalAmount,
        SalesDate
    FROM dbo.DummySales
    WHERE SalesDate BETWEEN @StartDate AND @EndDate
    ORDER BY SalesDate;
END;
GO

CREATE INDEX IX_AuditLogs_CreatedAt ON dbo.AuditLogs(CreatedAt DESC);
CREATE INDEX IX_Reports_OwnerUserId ON dbo.Reports(OwnerUserId);
CREATE INDEX IX_Reports_DatasourceId ON dbo.Reports(DatasourceId);
CREATE INDEX IX_DatasourceRoleAccess_RoleId ON dbo.DatasourceRoleAccess(RoleId);
CREATE INDEX IX_DatasourceUserAccess_UserId ON dbo.DatasourceUserAccess(UserId);
CREATE INDEX IX_ReportRoleAccess_RoleId ON dbo.ReportRoleAccess(RoleId);
CREATE INDEX IX_ReportUserAccess_UserId ON dbo.ReportUserAccess(UserId);
GO

/* Seed data */
INSERT INTO dbo.Roles (Name, Code, Description, CreatedBy)
VALUES
('Administrator', 'Admin', 'System administrator', 'seed'),
('IT', 'IT', 'Technical admin', 'seed'),
('Report User', 'ReportUser', 'Standard report consumer', 'seed');

INSERT INTO dbo.Permissions (Name, Code, Description, CreatedBy)
VALUES
('Manage Users', 'ManageUsers', 'Create/edit/deactivate users', 'seed'),
('Manage Roles', 'ManageRoles', 'Manage role definitions', 'seed'),
('Manage Permissions', 'ManagePermissions', 'Manage permission definitions', 'seed'),
('Manage Datasource', 'ManageDatasource', 'Create/edit datasource', 'seed'),
('View Datasource', 'ViewDatasource', 'View datasource list', 'seed'),
('Use Datasource', 'UseDatasource', 'Run datasource', 'seed'),
('Manage Report', 'ManageReport', 'Create/update/delete reports', 'seed'),
('View Report', 'ViewReport', 'View report metadata', 'seed'),
('Run Report', 'RunReport', 'Execute reports', 'seed'),
('Export Report', 'ExportReport', 'Export PDF/Excel', 'seed'),
('View Audit Logs', 'ViewAuditLogs', 'View audit log events', 'seed');

INSERT INTO dbo.Users (Username, Email, PasswordHash, FullName, CreatedBy)
VALUES ('admin', 'admin@adhoc.local', '$2a$11$RUiEr/0GH5w2AOehWUs8QOMt6OtWSI6oB6FSP205VdeNGc/EamCxq', 'System Admin', 'seed');

INSERT INTO dbo.UserRoles (UserId, RoleId, CreatedBy)
SELECT 1, Id, 'seed' FROM dbo.Roles WHERE Code = 'Admin';

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'seed'
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.Code = 'Admin';

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'seed'
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN ('ManageDatasource', 'ViewDatasource', 'UseDatasource', 'ViewReport', 'RunReport', 'ExportReport')
WHERE r.Code = 'IT';

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'seed'
FROM dbo.Roles r
JOIN dbo.Permissions p ON p.Code IN ('ViewDatasource', 'UseDatasource', 'ViewReport', 'RunReport', 'ExportReport')
WHERE r.Code = 'ReportUser';

INSERT INTO dbo.Datasources (Name, Code, Description, DatasourceType, SqlDefinitionOrObjectName, ConnectionName, CreatedBy)
VALUES
('Employees Listing', 'DS_EMP_LIST', 'View-based employee listing', 2, 'vw_EmployeeList', 'DefaultConnection', 'seed'),
('Sales by Date', 'DS_SALES_DATE', 'Procedure for sales date range', 3, 'sp_Report_SalesByDate', 'DefaultConnection', 'seed');

INSERT INTO dbo.DatasourceParameters (DatasourceId, Name, Label, DataType, IsRequired, DefaultValue, CreatedBy)
VALUES
(2, 'StartDate', 'Start Date', 'date', 1, NULL, 'seed'),
(2, 'EndDate', 'End Date', 'date', 1, NULL, 'seed');

INSERT INTO dbo.DatasourceColumnsMetadata (DatasourceId, ColumnName, DataType, IsAllowed, CreatedBy)
VALUES
(1, 'EmployeeId', 'int', 1, 'seed'),
(1, 'EmployeeCode', 'string', 1, 'seed'),
(1, 'FullName', 'string', 1, 'seed'),
(1, 'Department', 'string', 1, 'seed'),
(1, 'Salary', 'decimal', 1, 'seed'),
(1, 'JoiningDate', 'date', 1, 'seed');

INSERT INTO dbo.DatasourceRoleAccess (DatasourceId, RoleId, CanView, CanUse, CanManage, CreatedBy)
SELECT 1, Id, 1, 1, 0, 'seed' FROM dbo.Roles WHERE Code IN ('ReportUser', 'IT')
UNION ALL
SELECT 2, Id, 1, 1, 1, 'seed' FROM dbo.Roles WHERE Code = 'IT';

INSERT INTO dbo.Reports (Name, Code, Description, DatasourceId, OwnerUserId, IsPublic, IsPrivate, CreatedBy)
VALUES ('Employee Summary', 'RPT_EMP_SUM', 'Default employee seeded report', 1, 1, 1, 0, 'seed');

INSERT INTO dbo.ReportColumns (ReportId, ColumnName, DisplayName, DisplayOrder, CreatedBy)
VALUES
(1, 'EmployeeCode', 'Employee Code', 1, 'seed'),
(1, 'FullName', 'Employee Name', 2, 'seed'),
(1, 'Department', 'Department', 3, 'seed'),
(1, 'Salary', 'Salary', 4, 'seed');

INSERT INTO dbo.ReportBranding (ReportId, Title, Subtitle, HeaderAlignment, ShowLogo, ShowGeneratedDate, ShowGeneratedBy, FooterText, CreatedBy)
VALUES (1, 'Employee Summary', 'Seeded default report', 'Left', 0, 1, 1, 'Confidential - Internal Use', 'seed');

INSERT INTO dbo.SystemSettings (Category, SettingKey, SettingValue, Description, CreatedBy)
VALUES
('Export', 'MaxExportRows', '50000', 'Maximum rows allowed in report export', 'seed'),
('Report', 'MaxPreviewRows', '1000', 'Maximum rows allowed in report preview', 'seed'),
('Branding', 'CompanyName', 'Contoso Holdings', 'Default company name for report branding', 'seed'),
('Branding', 'Address', 'Main Boulevard, Lahore', 'Default company address for report branding', 'seed'),
('Branding', 'Phone', '+92-300-0000000', 'Default phone for report branding', 'seed'),
('Branding', 'Email', 'info@contoso.local', 'Default email for report branding', 'seed'),
('Branding', 'FooterText', 'Confidential - Internal Use', 'Default footer text for report branding', 'seed'),
('Branding', 'CompanyLogoDataUrl', '', 'Default logo (data URL) for report branding', 'seed'),
('Datasource', 'ExternalConnectionString', '', 'Optional external connection string for datasource/report runtime', 'seed');

INSERT INTO dbo.DummyEmployees (EmployeeCode, FullName, Department, Salary, JoiningDate)
VALUES
('EMP-001', 'Ali Raza', 'Finance', 120000, '2021-01-10'),
('EMP-002', 'Sara Khan', 'IT', 140000, '2022-03-15'),
('EMP-003', 'Usman Tariq', 'HR', 90000, '2020-08-20'),
('EMP-004', 'Ayesha Noor', 'Operations', 110000, '2023-02-01'),
('EMP-005', 'Hamza Iqbal', 'Sales', 100000, '2019-11-05');

INSERT INTO dbo.DummySales (Branch, ProductName, Quantity, UnitPrice, SalesDate)
VALUES
('Lahore', 'Laptop', 5, 250000, '2026-01-05'),
('Karachi', 'Printer', 10, 45000, '2026-01-07'),
('Islamabad', 'Router', 20, 12000, '2026-01-12'),
('Lahore', 'Monitor', 15, 60000, '2026-02-01'),
('Karachi', 'Keyboard', 50, 3500, '2026-02-18');
GO
