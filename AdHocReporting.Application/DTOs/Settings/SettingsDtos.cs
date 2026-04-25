namespace AdHocReporting.Application.DTOs.Settings;

public record BrandingSettingsDto(
    string CompanyName,
    string Address,
    string Phone,
    string Email,
    string FooterText,
    string? CompanyLogoDataUrl);

public record DatasourceSettingsDto(
    string? MaskedExternalConnectionString,
    bool IsUsingExternalConnection);

public record AiChatSettingsDto(
    string? MaskedApiKey,
    bool HasApiKey,
    string PlannerModel,
    string ResponderModel,
    IReadOnlyCollection<string> AvailableModels);

public record SystemSettingsDto(
    BrandingSettingsDto Branding,
    DatasourceSettingsDto Datasource,
    AiChatSettingsDto AiChat);

public record UpdateBrandingSettingsRequest(
    string CompanyName,
    string Address,
    string Phone,
    string Email,
    string FooterText,
    string? CompanyLogoDataUrl);

public record UpdateDatasourceSettingsRequest(
    string? ExternalConnectionString,
    bool ClearExternalConnectionString = false);

public record UpdateAiChatSettingsRequest(
    string? ApiKey,
    bool ClearApiKey,
    string PlannerModel,
    string ResponderModel);

public record TestDatasourceConnectionRequest(string? ExternalConnectionString);

public record DatasourceConnectionTestResultDto(
    bool IsSuccess,
    string Message,
    string? DataSource,
    string? Database);