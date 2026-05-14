namespace CreatorPlatform.Auth.Domain.Roles;

public sealed class Role
{
    private Role()
    {
    }

    private Role(short id, string name)
    {
        Id = id;
        Name = name;
    }

    public short Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    // id nije dinamicki vec se upisuje rucno
    // jer su role fiksne
    public static Role Create(short id, string name)
    {
        return new Role(id, name);
    }
}
