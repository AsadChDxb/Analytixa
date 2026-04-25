using AdHocReporting.Application.DTOs.Auth;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using AdHocReporting.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AdHocReporting.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly AdHocDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IAuditService _auditService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        AdHocDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IAuditService auditService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _auditService = auditService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => (x.Username == request.UsernameOrEmail || x.Email == request.UsernameOrEmail) && x.IsActive && !x.IsDeleted, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            await _auditService.LogAsync(null, "LoginFailed", "User", null, request.UsernameOrEmail, ipAddress, "anonymous", cancellationToken);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var roles = user.UserRoles.Select(x => x.Role!.Code).Distinct().ToList();
        var permissions = await _dbContext.RolePermissions
            .Where(x => roles.Contains(x.Role!.Code) && x.IsActive && !x.IsDeleted)
            .Select(x => x.Permission!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Username, roles, permissions, accessTokenExpiresAt);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedByIp = ipAddress,
            CreatedBy = user.Username
        });

        user.LastLoginAt = DateTime.UtcNow;
        user.ModifiedAt = DateTime.UtcNow;
        user.ModifiedBy = user.Username;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(user.Id, "Login", "User", user.Id.ToString(), null, ipAddress, user.Username, cancellationToken);

        return new AuthResultDto(user.Id, user.Username, user.FullName, accessToken, refreshToken, accessTokenExpiresAt, roles, permissions);
    }

    public async Task<AuthResultDto> RefreshAsync(RefreshTokenRequest request, string ipAddress, CancellationToken cancellationToken = default)
    {
        var token = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken && !x.IsDeleted, cancellationToken);

        if (token is null || !token.IsUsable || token.User is null || !token.User.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        token.ModifiedAt = DateTime.UtcNow;
        token.ModifiedBy = token.User.Username;

        var roles = await _dbContext.UserRoles.Where(x => x.UserId == token.UserId)
            .Select(x => x.Role!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissions = await _dbContext.RolePermissions
            .Where(x => roles.Contains(x.Role!.Code))
            .Select(x => x.Permission!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var accessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var accessToken = _tokenGenerator.GenerateAccessToken(token.User.Id, token.User.Username, roles, permissions, accessTokenExpiresAt);
        var newRefreshToken = _tokenGenerator.GenerateRefreshToken();

        token.ReplacedByToken = newRefreshToken;
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedByIp = ipAddress,
            CreatedBy = token.User.Username
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(token.User.Id, token.User.Username, token.User.FullName, accessToken, newRefreshToken, accessTokenExpiresAt, roles, permissions);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is invalid.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.ModifiedAt = DateTime.UtcNow;
        user.ModifiedBy = user.Username;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => (x.Username == request.UsernameOrEmail || x.Email == request.UsernameOrEmail) && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = true;
        user.ModifiedAt = DateTime.UtcNow;
        user.ModifiedBy = "system-reset";

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
