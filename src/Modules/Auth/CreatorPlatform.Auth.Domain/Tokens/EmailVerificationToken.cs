namespace CreatorPlatform.Auth.Domain.Tokens;

public sealed class EmailVerificationToken
{
    private EmailVerificationToken()
    {
    }

    private EmailVerificationToken(
        Guid id,
        int userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public static EmailVerificationToken Create(
        int userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new EmailVerificationToken(
            Guid.NewGuid(),
            userId,
            tokenHash,
            expiresAt,
            createdAt);
    }

    public Guid Id { get; private set; }

    public int UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
