namespace AdHocReporting.Application.DTOs.Auth;

public record LoginRequest(string UsernameOrEmail, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ResetPasswordRequest(string UsernameOrEmail, string NewPassword, string ResetToken);

public record AuthResultDto(
    long UserId,
    string Username,
    string FullName,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
