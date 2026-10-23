using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class ContactImportRepository : IContactImportRepository
{
    // Bounded statement size: up to 10,000 emails in 10 inserts.
    private const int InsertBatchSize = 1_000;

    private readonly CreatorPlatformDbContext _context;

    public ContactImportRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<(HashSet<string> Contacts, HashSet<string> Unsubscribed)> ClassifyAsync(
        int creatorId, IReadOnlyCollection<string> emails, CancellationToken ct)
    {
        if (emails.Count == 0)
            return ([], []);

        var list = emails.ToArray();

        // Both probe their (CreatorId, Email) unique index per email of the file.
        var contacts = await _context.Database.SqlQuery<string>($"""
            SELECT cs."Email" AS "Value"
            FROM marketing.contact_summaries cs
            WHERE cs."CreatorId" = {creatorId} AND cs."Email" = ANY({list})
            """).ToListAsync(ct);

        var unsubscribed = await _context.Database.SqlQuery<string>($"""
            SELECT u."Email" AS "Value"
            FROM marketing.unsubscribes u
            WHERE u."CreatorId" = {creatorId} AND u."Email" = ANY({list})
            """).ToListAsync(ct);

        return (contacts.ToHashSet(StringComparer.Ordinal), unsubscribed.ToHashSet(StringComparer.Ordinal));
    }

    public async Task AddAsync(ContactImport import, CancellationToken ct)
    {
        await _context.Set<ContactImport>().AddAsync(import, ct);
    }

    public async Task<int> InsertContactsAsync(
        int creatorId, int importId, IReadOnlyList<string> emails, DateTimeOffset now, CancellationToken ct)
    {
        var inserted = 0;

        foreach (var batch in emails.Chunk(InsertBatchSize))
        {
            // The unsubscribe check is repeated here, in the insert itself, as a last guard: an unsubscribed email is
            // never imported. ON CONFLICT skips an email a concurrent sign-up created first; the affected-row count
            // is exactly what was inserted.
            inserted += await _context.Database.ExecuteSqlAsync($"""
                INSERT INTO marketing.contact_summaries
                    ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds", "ImportedAt", "ImportId")
                SELECT {creatorId}, e."Email", {now}, {now}, ARRAY[]::integer[], {now}, {importId}
                FROM unnest({batch}) AS e("Email")
                WHERE NOT EXISTS (
                    SELECT 1 FROM marketing.unsubscribes u
                    WHERE u."CreatorId" = {creatorId} AND u."Email" = e."Email"
                )
                ON CONFLICT ("CreatorId", "Email") DO NOTHING
                """, ct);
        }

        return inserted;
    }
}
