using AdHocReporting.Application.DTOs.Settings;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class SystemSettingsService : ISystemSettingsService
{
    private const string BrandingCategory = "Branding";
    private const string DatasourceCategory = "Datasource";
    private const string AiChatCategory = "AiChat";
    private static readonly string[] AvailableAiModels = ["gpt-5.4", "gpt-5.4-mini"];

    private readonly AdHocDbContext _dbContext;
    private readonly SettingsSecretProtectionService _settingsSecretProtectionService;

    public SystemSettingsService(AdHocDbContext dbContext, SettingsSecretProtectionService settingsSecretProtectionService)
    {
        _dbContext = dbContext;
        _settingsSecretProtectionService = settingsSecretProtectionService;
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var branding = new BrandingSettingsDto(
            await GetSettingValueAsync(BrandingCategory, "CompanyName", "Contoso Holdings", cancellationToken) ?? "Contoso Holdings",
            await GetSettingValueAsync(BrandingCategory, "Address", "Main Boulevard, Lahore", cancellationToken) ?? "Main Boulevard, Lahore",
            await GetSettingValueAsync(BrandingCategory, "Phone", "+92-300-0000000", cancellationToken) ?? "+92-300-0000000",
            await GetSettingValueAsync(BrandingCategory, "Email", "info@contoso.local", cancellationToken) ?? "info@contoso.local",
            await GetSettingValueAsync(BrandingCategory, "FooterText", "Confidential - Internal Use", cancellationToken) ?? "Confidential - Internal Use",
            await GetSettingValueAsync(BrandingCategory, "CompanyLogoDataUrl", null, cancellationToken));

        var storedConnection = await GetSettingValueAsync(DatasourceCategory, "ExternalConnectionString", null, cancellationToken);
        var externalConnectionString = _settingsSecretProtectionService.Unprotect(storedConnection);
        var datasource = new DatasourceSettingsDto(
            _settingsSecretProtectionService.MaskConnectionString(externalConnectionString),
            !string.IsNullOrWhiteSpace(externalConnectionString));

        var storedApiKey = await GetSettingValueAsync(AiChatCategory, "ApiKey", null, cancellationToken);
        var apiKey = _settingsSecretProtectionService.Unprotect(storedApiKey);
        var aiChat = new AiChatSettingsDto(
            _settingsSecretProtectionService.MaskSecret(apiKey),
            !string.IsNullOrWhiteSpace(apiKey),
            NormalizeAiModel(await GetSettingValueAsync(AiChatCategory, "PlannerModel", "gpt-5.4", cancellationToken), "gpt-5.4"),
            NormalizeAiModel(await GetSettingValueAsync(AiChatCategory, "ResponderModel", "gpt-5.4-mini", cancellationToken), "gpt-5.4-mini"),
            AvailableAiModels);

        return new SystemSettingsDto(branding, datasource, aiChat);
    }

    public async Task<BrandingSettingsDto> UpdateBrandingSettingsAsync(UpdateBrandingSettingsRequest request, string actor, CancellationToken cancellationToken = default)
    {
        await UpsertSettingAsync(BrandingCategory, "CompanyName", request.CompanyName.Trim(), "Default company name for report branding", actor, cancellationToken);
        await UpsertSettingAsync(BrandingCategory, "Address", request.Address.Trim(), "Default company address for report branding", actor, cancellationToken);
        await UpsertSettingAsync(BrandingCategory, "Phone", request.Phone.Trim(), "Default phone for report branding", actor, cancellationToken);
        await UpsertSettingAsync(BrandingCategory, "Email", request.Email.Trim(), "Default email for report branding", actor, cancellationToken);
        await UpsertSettingAsync(BrandingCategory, "FooterText", request.FooterText.Trim(), "Default footer text for report branding", actor, cancellationToken);
        await UpsertSettingAsync(BrandingCategory, "CompanyLogoDataUrl", request.CompanyLogoDataUrl?.Trim() ?? string.Empty, "Default logo (data URL) for report branding", actor, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BrandingSettingsDto(
            request.CompanyName.Trim(),
            request.Address.Trim(),
            request.Phone.Trim(),
            request.Email.Trim(),
            request.FooterText.Trim(),
            string.IsNullOrWhiteSpace(request.CompanyLogoDataUrl) ? null : request.CompanyLogoDataUrl.Trim());
    }

    public async Task<DatasourceSettingsDto> UpdateDatasourceSettingsAsync(UpdateDatasourceSettingsRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var existingStoredConnection = await GetSettingValueAsync(DatasourceCategory, "ExternalConnectionString", string.Empty, cancellationToken) ?? string.Empty;

        var nextStoredConnection = existingStoredConnection;
        if (request.ClearExternalConnectionString)
        {
            nextStoredConnection = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(request.ExternalConnectionString))
        {
            var normalizedConnectionString = NormalizeConnectionString(request.ExternalConnectionString);
            nextStoredConnection = _settingsSecretProtectionService.Protect(normalizedConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(existingStoredConnection) && !_settingsSecretProtectionService.IsProtected(existingStoredConnection))
        {
            nextStoredConnection = _settingsSecretProtectionService.Protect(existingStoredConnection);
        }

        await UpsertSettingAsync(
            DatasourceCategory,
            "ExternalConnectionString",
            nextStoredConnection,
            "Optional external connection string for datasource/report runtime",
            actor,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var decryptedConnection = _settingsSecretProtectionService.Unprotect(nextStoredConnection);

        return new DatasourceSettingsDto(
            _settingsSecretProtectionService.MaskConnectionString(decryptedConnection),
            !string.IsNullOrWhiteSpace(decryptedConnection));
    }

    public async Task<AiChatSettingsDto> UpdateAiChatSettingsAsync(UpdateAiChatSettingsRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var existingStoredKey = await GetSettingValueAsync(AiChatCategory, "ApiKey", string.Empty, cancellationToken) ?? string.Empty;

        var nextStoredKey = existingStoredKey;
        if (request.ClearApiKey)
        {
            nextStoredKey = string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            nextStoredKey = _settingsSecretProtectionService.Protect(request.ApiKey.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(existingStoredKey) && !_settingsSecretProtectionService.IsProtected(existingStoredKey))
        {
            nextStoredKey = _settingsSecretProtectionService.Protect(existingStoredKey);
        }

        var plannerModel = NormalizeAiModel(request.PlannerModel, "gpt-5.4");
        var responderModel = NormalizeAiModel(request.ResponderModel, "gpt-5.4-mini");

        await UpsertSettingAsync(AiChatCategory, "ApiKey", nextStoredKey, "Protected OpenAI API key for Nexa assistant", actor, cancellationToken);
        await UpsertSettingAsync(AiChatCategory, "PlannerModel", plannerModel, "Planning model for datasource selection", actor, cancellationToken);
        await UpsertSettingAsync(AiChatCategory, "ResponderModel", responderModel, "Streaming responder model for Nexa assistant", actor, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var decryptedApiKey = _settingsSecretProtectionService.Unprotect(nextStoredKey);
        return new AiChatSettingsDto(
            _settingsSecretProtectionService.MaskSecret(decryptedApiKey),
            !string.IsNullOrWhiteSpace(decryptedApiKey),
            plannerModel,
            responderModel,
            AvailableAiModels);
    }

    public async Task<DatasourceConnectionTestResultDto> TestDatasourceConnectionAsync(TestDatasourceConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var candidateConnectionString = NormalizeConnectionString(request.ExternalConnectionString);
        if (string.IsNullOrWhiteSpace(candidateConnectionString))
        {
            var storedConnection = await GetSettingValueAsync(DatasourceCategory, "ExternalConnectionString", null, cancellationToken);
            candidateConnectionString = NormalizeConnectionString(_settingsSecretProtectionService.Unprotect(storedConnection));
        }

        if (string.IsNullOrWhiteSpace(candidateConnectionString))
        {
            return new DatasourceConnectionTestResultDto(false, "No external connection string provided or configured.", null, null);
        }

        try
        {
            await using var connection = new SqlConnection(candidateConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            var endpoint = _settingsSecretProtectionService.TryExtractConnectionEndpoint(candidateConnectionString);
            return new DatasourceConnectionTestResultDto(
                true,
                "Connection successful.",
                endpoint.Server,
                endpoint.Database);
        }
        catch (Exception ex)
        {
            return new DatasourceConnectionTestResultDto(false, $"Connection failed: {ex.Message}", null, null);
        }
    }

    private static string NormalizeConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim();

        if ((normalized.StartsWith('"') && normalized.EndsWith('"')) || (normalized.StartsWith('\'') && normalized.EndsWith('\'')))
        {
            normalized = normalized[1..^1].Trim();
        }

        while (normalized.Contains("\\\\", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return normalized;
    }

    private async Task<string?> GetSettingValueAsync(string category, string key, string? defaultValue, CancellationToken cancellationToken)
    {
        var value = await _dbContext.SystemSettings
            .Where(x => x.Category == category && x.SettingKey == key && x.IsActive && !x.IsDeleted)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value;
    }

    private async Task UpsertSettingAsync(string category, string key, string value, string description, string actor, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.SystemSettings
            .FirstOrDefaultAsync(x => x.Category == category && x.SettingKey == key, cancellationToken);

        if (existing is null)
        {
            _dbContext.SystemSettings.Add(new SystemSetting
            {
                Category = category,
                SettingKey = key,
                SettingValue = value,
                Description = description,
                IsActive = true,
                IsDeleted = false,
                CreatedBy = actor
            });

            return;
        }

        existing.SettingValue = value;
        existing.Description = description;
        existing.IsActive = true;
        existing.IsDeleted = false;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedBy = actor;
    }

    private static string NormalizeAiModel(string? requested, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
        return AvailableAiModels.Contains(candidate, StringComparer.OrdinalIgnoreCase)
            ? AvailableAiModels.First(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            : fallback;
    }
}