using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Application.Interfaces;

public sealed record UserWithRoles(User User, IReadOnlyList<string> Roles);

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Same lookup as <see cref="GetByIdAsync"/> but also resolves the user's role names in the
    /// same round trip (LEFT JOIN auth.user_roles/auth.roles) — used by session authentication so
    /// role-gated endpoints don't need a second query per request.</summary>
    Task<UserWithRoles?> GetByIdWithRolesAsync(int id, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
