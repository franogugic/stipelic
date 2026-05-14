namespace CreatorPlatform.Auth.Domain.Sessions;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(
        Guid id,
        int userId,
        string sessionTokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        SessionTokenHash = sessionTokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public static UserSession Create(
        int userId,
        string sessionTokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new UserSession(
            Guid.NewGuid(),
            userId,
            sessionTokenHash,
            expiresAt,
            createdAt);
    }

    public Guid Id { get; private set; }

    public int UserId { get; private set; }

    public string SessionTokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
