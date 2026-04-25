namespace AdHocReporting.Application.DTOs.Users;

public record UserDto(long Id, string Username, string Email, string FullName, bool IsActive, IReadOnlyCollection<string> Roles);

public record CreateUserRequest(string Username, string Email, string FullName, string Password, IReadOnlyCollection<long> RoleIds);

public record UpdateUserRequest(string Email, string FullName, bool IsActive, IReadOnlyCollection<long> RoleIds);
