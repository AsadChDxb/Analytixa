using AdHocReporting.Application.Common;
using AdHocReporting.Application.DTOs.Users;
using AdHocReporting.Application.Interfaces;
using AdHocReporting.Domain.Entities;
using AdHocReporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdHocReporting.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly AdHocDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(AdHocDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<PaginatedResult<UserDto>> GetUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var users = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PaginatedResult<UserDto>
        {
            Items = users.Select(x => new UserDto(x.Id, x.Username, x.Email, x.FullName, x.IsActive, x.UserRoles.Select(y => y.Role!.Code).ToList())).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Users.AnyAsync(x => x.Username == request.Username || x.Email == request.Email, cancellationToken))
        {
            throw new InvalidOperationException("Username or email already exists.");
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedBy = actor
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var roleId in request.RoleIds.Distinct())
        {
            _dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId, CreatedBy = actor });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var roleCodes = await _dbContext.UserRoles.Where(x => x.UserId == user.Id).Select(x => x.Role!.Code).ToListAsync(cancellationToken);
        return new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsActive, roleCodes);
    }

    public async Task<UserDto> UpdateUserAsync(long userId, UpdateUserRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.Email = request.Email;
        user.FullName = request.FullName;
        user.IsActive = request.IsActive;
        user.ModifiedAt = DateTime.UtcNow;
        user.ModifiedBy = actor;

        _dbContext.UserRoles.RemoveRange(user.UserRoles);
        foreach (var roleId in request.RoleIds.Distinct())
        {
            _dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId, CreatedBy = actor });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var roleCodes = await _dbContext.UserRoles.Where(x => x.UserId == user.Id).Select(x => x.Role!.Code).ToListAsync(cancellationToken);
        return new UserDto(user.Id, user.Username, user.Email, user.FullName, user.IsActive, roleCodes);
    }

    public async Task SetUserActiveAsync(long userId, bool isActive, string actor, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = isActive;
        user.ModifiedAt = DateTime.UtcNow;
        user.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
