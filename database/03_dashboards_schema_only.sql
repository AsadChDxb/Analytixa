/*
Patch script: Dashboard schema only (table + indexes).
Safe for existing databases; idempotent.
*/

USE AdHocReportingDb;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dashboards (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Code NVARCHAR(100) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        DatasourceId BIGINT NOT NULL,
        OwnerUserId BIGINT NOT NULL,
        DefinitionJson NVARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy NVARCHAR(100) NULL,
        CONSTRAINT FK_Dashboards_Datasources FOREIGN KEY (DatasourceId) REFERENCES dbo.Datasources(Id),
        CONSTRAINT FK_Dashboards_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id)
    );
END;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.Dashboards')
         AND name = N'UQ_Dashboards_Code'
   )
BEGIN
    CREATE UNIQUE INDEX UQ_Dashboards_Code ON dbo.Dashboards(Code);
END;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.Dashboards')
         AND name = N'IX_Dashboards_OwnerUserId'
   )
BEGIN
    CREATE INDEX IX_Dashboards_OwnerUserId ON dbo.Dashboards(OwnerUserId);
END;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.Dashboards')
         AND name = N'IX_Dashboards_DatasourceId'
   )
BEGIN
    CREATE INDEX IX_Dashboards_DatasourceId ON dbo.Dashboards(DatasourceId);
END;
GO
