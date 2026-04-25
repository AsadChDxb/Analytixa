using System.Security.Claims;

namespace AdHocReporting.API.Extensions;

public static class HttpContextUserExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : 0;
    }

    public static string GetUsername(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("unique_name") ?? "anonymous";

    public static bool HasRole(this ClaimsPrincipal user, string roleCode) =>
        user.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == roleCode);
}
