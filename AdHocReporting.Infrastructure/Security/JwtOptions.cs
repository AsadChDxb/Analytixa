namespace AdHocReporting.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AdHocReporting";
    public string Audience { get; set; } = "AdHocReportingClient";
    public string Key { get; set; } = "CHANGE_ME_VERY_LONG_SECURE_KEY_2026";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}
