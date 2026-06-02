namespace CreatorPlatform.Auth.Domain.Roles;

public sealed class Role
{
    private Role()
    {
    }

    private Role(RoleId id, string name)
    {
        Id = id;
        Name = name;
    }

    public static Role Create(RoleId id, string name)
    {
        return new Role(id, name);
    }

    public RoleId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
}
