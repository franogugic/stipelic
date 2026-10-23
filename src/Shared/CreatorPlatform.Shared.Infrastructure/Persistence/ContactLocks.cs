using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Shared.Infrastructure.Persistence;

/// <summary>
/// A transaction-scoped advisory lock on one contact (creator + normalised email). The three paths that move the
/// <c>max_contacts</c> counter — a sign-up creating the contact, an unsubscribe and a contact delete — each decide by
/// reading the other paths' row (is the email unsubscribed? is it a contact?), so they take this lock first and can
/// never both miss each other. Released at commit or rollback.
/// </summary>
public static class ContactLocks
{
    // Hash seed: keeps these keys apart from other advisory locks (the campaign and payout locks use the two-int form).
    private const long Seed = 42003;

    public static async Task AcquireAsync(DbContext context, int creatorId, string normalizedEmail, CancellationToken ct)
    {
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({creatorId}::text || ':' || {normalizedEmail}, {Seed}))", ct);
    }
}
