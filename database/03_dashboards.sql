/*
Patch script: add dashboard persistence for chart/KPI dashboard builder.
This script is idempotent and safe to run multiple times.
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
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'dbo.Dashboards')
         AND i.is_unique = 1
         AND c.name = N'Code'
   )
BEGIN
    CREATE UNIQUE INDEX UQ_Dashboards_Code ON dbo.Dashboards(Code);
END;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'dbo.Dashboards')
         AND i.is_unique = 0
         AND c.name = N'OwnerUserId'
   )
BEGIN
    CREATE INDEX IX_Dashboards_OwnerUserId ON dbo.Dashboards(OwnerUserId);
END;
GO

IF OBJECT_ID(N'dbo.Dashboards', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes i
       JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'dbo.Dashboards')
         AND i.is_unique = 0
         AND c.name = N'DatasourceId'
   )
BEGIN
    CREATE INDEX IX_Dashboards_DatasourceId ON dbo.Dashboards(DatasourceId);
END;
GO

DECLARE @DatasourceId BIGINT = (
    SELECT TOP (1) d.Id
    FROM dbo.Datasources d
    WHERE d.Code = N'DS_EMP_LIST' AND d.IsDeleted = 0
    ORDER BY d.Id
);

DECLARE @OwnerUserId BIGINT = (
    SELECT TOP (1) u.Id
    FROM dbo.Users u
    WHERE u.Username = N'admin' AND u.IsDeleted = 0
    ORDER BY u.Id
);

IF @DatasourceId IS NOT NULL
   AND @OwnerUserId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Dashboards WHERE Code = N'DSH_EXEC_SNAPSHOT')
BEGIN
    INSERT INTO dbo.Dashboards
    (
        Name,
        Code,
        Description,
        DatasourceId,
        OwnerUserId,
        DefinitionJson,
        IsActive,
        IsDeleted,
        CreatedAt,
        CreatedBy
    )
    VALUES
    (
        N'Executive Snapshot',
        N'DSH_EXEC_SNAPSHOT',
        N'Seeded dashboard with KPI tiles and a chart.',
        @DatasourceId,
        @OwnerUserId,
        N'{"filters":[],"widgets":[{"id":"seed-kpi","type":"kpi","title":"Employees","layout":{"columnStart":1,"columnSpan":4,"rowSpan":1,"minHeight":180},"config":{"metric":"count","label":"Total employees","accent":"#3aa96b"}},{"id":"seed-chart","type":"bar","title":"Employees by Department","layout":{"columnStart":1,"columnSpan":8,"rowSpan":2,"minHeight":320},"config":{"xField":"Department","yField":"EmployeeId","aggregate":"count","showLegend":false,"xLabel":"Department","yLabel":"Employees","accent":"#ff7a59"}}],"theme":{"palette":["#3aa96b","#ff7a59","#4c7fff","#f6bd16"]}}',
        1,
        0,
        SYSUTCDATETIME(),
        N'script'
    );
END;
GO
