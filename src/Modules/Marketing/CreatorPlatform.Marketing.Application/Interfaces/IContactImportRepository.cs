using CreatorPlatform.Marketing.Domain.Contacts;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IContactImportRepository
{
    /// <summary>Which of <paramref name="emails"/> (normalised) are the creator's contacts already, and which are
    /// unsubscribed — two set queries, however long the list.</summary>
    Task<(HashSet<string> Contacts, HashSet<string> Unsubscribed)> ClassifyAsync(
        int creatorId, IReadOnlyCollection<string> emails, CancellationToken ct);

    Task AddAsync(ContactImport import, CancellationToken ct);

    /// <summary>Creates the contacts in batches: <c>FirstCapturedAt = LastCapturedAt = ImportedAt = now</c>, no source
    /// pages, linked to the import. Skips an email that is unsubscribed or already a contact (<c>ON CONFLICT DO
    /// NOTHING</c> — a concurrent sign-up may have won) and returns how many were really inserted.</summary>
    Task<int> InsertContactsAsync(
        int creatorId, int importId, IReadOnlyList<string> emails, DateTimeOffset now, CancellationToken ct);
}
