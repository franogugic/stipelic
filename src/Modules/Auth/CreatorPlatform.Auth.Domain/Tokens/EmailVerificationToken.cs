using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Domain.Tokens;

public sealed class EmailVerificationToken
{
    private EmailVerificationToken()
    {
    }

    private EmailVerificationToken(
        Guid id,
        User user,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        User = user;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public static EmailVerificationToken Create(
        User user,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new EmailVerificationToken(
            Guid.NewGuid(),
            user,
            tokenHash,
            expiresAt,
            createdAt);
    }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt <= now;
    }

    public void MarkAsUsed(DateTimeOffset usedAt)
    {
        UsedAt = usedAt;
    }

    public Guid Id { get; private set; }

    public int UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    
    public User User { get; private set; } = null!;
}
