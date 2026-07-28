namespace CreatorPlatform.Marketing.Domain.Unsubscribes;

/// <summary>A creator-scoped suppression: once a recipient unsubscribes from one of a creator's
/// campaigns, they're excluded from ALL of that creator's future campaigns (unsubscribe is per-creator,
/// not per-campaign — GDPR requirement). Insert follows the same idempotent
/// <c>ON CONFLICT DO NOTHING</c> pattern as the analytics tables — see <see cref="Interfaces.IUnsubscribeRepository.AddIfNotExistsAsync"/>.</summary>
public sealed class Unsubscribe
{
    private Unsubscribe()
    {
    }

    private Unsubscribe(int creatorId, string email, UnsubscribeSource source, DateTimeOffset unsubscribedAt)
    {
        CreatorId = creatorId;
        Email = email;
        Source = source;
        UnsubscribedAt = unsubscribedAt;
    }

    public static Unsubscribe Create(int creatorId, string email, UnsubscribeSource source, DateTimeOffset unsubscribedAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new Unsubscribe(creatorId, email.Trim().ToLowerInvariant(), source, unsubscribedAt);
    }

    public int Id { get; private set; }

    public int CreatorId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public UnsubscribeSource Source { get; private set; }

    public DateTimeOffset UnsubscribedAt { get; private set; }
}
