using CreatorPlatform.Auth.Application.Authorization;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

/// <summary>
/// Exercises the exact <see cref="RoleGuard"/> call the (Api-project) AdminPayoutsController.GetPlatformAdmin()
/// helper makes — kept out-of-process from ASP.NET controller wiring so this stays a fast, dependency-free
/// unit test instead of pulling the whole Api project (and its transitive Infrastructure graph) into the
/// test project.
/// </summary>
public class AdminPayoutsControllerTests
{
    private const string PlatformAdminRole = "platform_admin";

    private static CurrentUserDto BuildUser(params string[] roles) => new()
    {
        Id = 1,
        Email = "user@example.com",
        IsEmailVerified = true,
        Status = "Active",
        Roles = roles
    };

    [Fact]
    public void RequireRole_UserWithoutPlatformAdminRole_ThrowsForbidden()
    {
        var user = BuildUser("user");

        Assert.Throws<ForbiddenException>(() => RoleGuard.RequireRole(user, PlatformAdminRole));
    }

    [Fact]
    public void RequireRole_UserWithPlatformAdminRole_DoesNotThrow()
    {
        var user = BuildUser("user", PlatformAdminRole);

        RoleGuard.RequireRole(user, PlatformAdminRole);
    }
}
