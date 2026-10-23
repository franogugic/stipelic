using System.Security.Cryptography;

namespace CreatorPlatform.Products.Domain.Access;

/// <summary>
/// A personal link to a product's document: <c>{FrontendBaseUrl}/d/{Token}</c>. Created for a paid order (in the payment
/// webhook's transaction) or for a new sign-up on a lead page whose product has a file. Exactly one of
/// <see cref="OrderId"/> / <see cref="EmailCaptureId"/> is set. A link doesn't expire; a refund turns it off
/// (<see cref="RevokedAt"/>). Each download goes through a fresh short-lived signed URL, always to the product's current
/// file. The download counters are bumped only by the download endpoint's single statement.
/// </summary>
public sealed class AccessGrant
{
    public const int TokenLength = 43;

    private AccessGrant()
    {
    }

    private AccessGrant(int creatorId, int productId, int? orderId, Guid? emailCaptureId, string email, DateTimeOffset createdAt)
    {
        Token = NewToken();
        CreatorId = creatorId;
        ProductId = productId;
        OrderId = orderId;
        EmailCaptureId = emailCaptureId;
        Email = email.Trim().ToLowerInvariant();
        CreatedAt = createdAt;
    }

    public static AccessGrant ForOrder(int creatorId, int productId, int orderId, string email, DateTimeOffset createdAt) =>
        new(creatorId, productId, orderId, null, RequireEmail(email), createdAt);

    public static AccessGrant ForCapture(int creatorId, int productId, Guid emailCaptureId, string email, DateTimeOffset createdAt) =>
        new(creatorId, productId, null, emailCaptureId, RequireEmail(email), createdAt);

    /// <summary>32 random bytes, base64url without padding: 43 characters, unguessable.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }

    /// <summary>The link was emailed (lead magnets: a repeat sign-up re-sends at most once per 24 hours).</summary>
    public void MarkSent(DateTimeOffset sentAt)
    {
        LastSentAt = sentAt;
    }

    private static string RequireEmail(string email) =>
        string.IsNullOrWhiteSpace(email) ? throw new ArgumentException("An access grant needs an email.", nameof(email)) : email;

    public int Id { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public int CreatorId { get; private set; }

    public int ProductId { get; private set; }

    public int? OrderId { get; private set; }

    public Guid? EmailCaptureId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }

    public int DownloadCount { get; private set; }

    public DateTimeOffset? LastDownloadedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;
}
