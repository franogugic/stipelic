using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Auth.Application.Authorization;

/// <summary>Shared role check used by controller-level <c>GetPlatformAdmin()</c>-style helpers — kept as a
/// single reusable function (rather than duplicated per controller like <c>GetVerifiedUser</c>) so the
/// 403 behavior is independently testable without spinning up a controller.</summary>
public static class RoleGuard
{
    public static void RequireRole(CurrentUserDto user, string role)
    {
        if (!user.Roles.Contains(role))
            throw new ForbiddenException($"'{role}' role required.");
    }
}
