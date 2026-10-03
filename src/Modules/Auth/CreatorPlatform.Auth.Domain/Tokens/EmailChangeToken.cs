using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Domain.Tokens;

/// <summary>A pending change of a user's sign-in email: proves the user controls <see cref="NewEmail"/> once the
/// link mailed there is opened. Only the hash of the token is stored.</summary>
public sealed class EmailChangeToken
{
    private EmailChangeToken()
    {
    }

    private EmailChangeToken(
        Guid id,
        User user,
        string newEmail,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        User = user;
        NewEmail = newEmail;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public static EmailChangeToken Create(
        User user,
        string newEmail,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new EmailChangeToken(Guid.NewGuid(), user, newEmail, tokenHash, expiresAt, createdAt);
    }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public void MarkAsUsed(DateTimeOffset usedAt)
    {
        UsedAt = usedAt;
    }

    /// <summary>Retires a token that was never used (superseded by a newer request, or by a completed change).</summary>
    public void Invalidate(DateTimeOffset invalidatedAt)
    {
        UsedAt = invalidatedAt;
    }

    public Guid Id { get; private set; }

    public int UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>Normalised (trimmed, lowercased).</summary>
    public string NewEmail { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
