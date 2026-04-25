/*
Patch script: add branding + external datasource settings keys for existing databases.
This script is idempotent and safe to run multiple times.
*/

USE AdHocReportingDb;
GO

MERGE dbo.SystemSettings AS target
USING (
    SELECT N'Branding' AS Category, N'CompanyName' AS SettingKey, N'Contoso Holdings' AS SettingValue, N'Default company name for report branding' AS [Description]
    UNION ALL SELECT N'Branding', N'Address', N'Main Boulevard, Lahore', N'Default company address for report branding'
    UNION ALL SELECT N'Branding', N'Phone', N'+92-300-0000000', N'Default phone for report branding'
    UNION ALL SELECT N'Branding', N'Email', N'info@contoso.local', N'Default email for report branding'
    UNION ALL SELECT N'Branding', N'FooterText', N'Confidential - Internal Use', N'Default footer text for report branding'
    UNION ALL SELECT N'Branding', N'CompanyLogoDataUrl', N'', N'Default logo (data URL) for report branding'
    UNION ALL SELECT N'Datasource', N'ExternalConnectionString', N'', N'Optional external connection string for datasource/report runtime'
) AS src
ON target.Category = src.Category AND target.SettingKey = src.SettingKey
WHEN MATCHED THEN
    UPDATE SET
        target.Description = src.[Description],
        target.IsActive = 1,
        target.IsDeleted = 0,
        target.ModifiedAt = SYSUTCDATETIME(),
        target.ModifiedBy = N'script'
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Category, SettingKey, SettingValue, Description, IsActive, IsDeleted, CreatedAt, CreatedBy)
    VALUES (src.Category, src.SettingKey, src.SettingValue, src.[Description], 1, 0, SYSUTCDATETIME(), N'script');
GO
