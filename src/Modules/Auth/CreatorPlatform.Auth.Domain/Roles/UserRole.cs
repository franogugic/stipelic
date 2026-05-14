namespace CreatorPlatform.Auth.Domain.Roles;

public sealed class UserRole
{
    private UserRole()
    {
    }

    private UserRole(int userId, short roleId, DateTimeOffset createdAt)
    {
        UserId = userId;
        RoleId = roleId;
        CreatedAt = createdAt;
    }

    public static UserRole Create(int userId, short roleId, DateTimeOffset createdAt)
    {
        return new UserRole(userId, roleId, createdAt);
    }

    public int UserId { get; private set; }

    public short RoleId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
