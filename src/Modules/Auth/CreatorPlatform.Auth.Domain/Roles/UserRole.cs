using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Domain.Roles;

public sealed class UserRole
{
    private UserRole()
    {
    }

    private UserRole(User user, RoleId roleId, DateTimeOffset createdAt)
    {
        User = user;
        RoleId = roleId;
        CreatedAt = createdAt;
    }

    public static UserRole Create(User user, RoleId roleId, DateTimeOffset createdAt)
    {
        return new UserRole(user, roleId, createdAt);
    }

    public int UserId { get; private set; }

    public RoleId RoleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public User User { get; private set; } = null!;
}
