using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Persistence;

public static class RuntimeSchemaInitializer
{
    public static Task EnsureAiChatSchemaAsync(AdHocDbContext dbContext, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.AiChatSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiChatSessions (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId BIGINT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AiChatSessions_IsActive DEFAULT (1),
        IsDeleted BIT NOT NULL CONSTRAINT DF_AiChatSessions_IsDeleted DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiChatSessions_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(150) NOT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy NVARCHAR(150) NULL,
        CONSTRAINT FK_AiChatSessions_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_AiChatSessions_UserId_CreatedAt ON dbo.AiChatSessions(UserId, CreatedAt);
END;

IF OBJECT_ID(N'dbo.AiChatMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiChatMessages (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SessionId BIGINT NOT NULL,
        Role NVARCHAR(30) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        MetadataJson NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AiChatMessages_IsActive DEFAULT (1),
        IsDeleted BIT NOT NULL CONSTRAINT DF_AiChatMessages_IsDeleted DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiChatMessages_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(150) NOT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy NVARCHAR(150) NULL,
        CONSTRAINT FK_AiChatMessages_AiChatSessions_SessionId FOREIGN KEY (SessionId) REFERENCES dbo.AiChatSessions(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AiChatMessages_SessionId_CreatedAt ON dbo.AiChatMessages(SessionId, CreatedAt);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE Category = N'AiChat' AND SettingKey = N'ApiKey')
BEGIN
    INSERT INTO dbo.SystemSettings (Category, SettingKey, SettingValue, Description, IsActive, IsDeleted, CreatedAt, CreatedBy)
    VALUES (N'AiChat', N'ApiKey', N'', N'Protected OpenAI API key for Nexa assistant', 1, 0, SYSUTCDATETIME(), N'system');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE Category = N'AiChat' AND SettingKey = N'PlannerModel')
BEGIN
    INSERT INTO dbo.SystemSettings (Category, SettingKey, SettingValue, Description, IsActive, IsDeleted, CreatedAt, CreatedBy)
    VALUES (N'AiChat', N'PlannerModel', N'gpt-5.4', N'Planning model for datasource selection', 1, 0, SYSUTCDATETIME(), N'system');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE Category = N'AiChat' AND SettingKey = N'ResponderModel')
BEGIN
    INSERT INTO dbo.SystemSettings (Category, SettingKey, SettingValue, Description, IsActive, IsDeleted, CreatedAt, CreatedBy)
    VALUES (N'AiChat', N'ResponderModel', N'gpt-5.4-mini', N'Streaming responder model for Nexa assistant', 1, 0, SYSUTCDATETIME(), N'system');
END;
""";

        return dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}