namespace CreatorPlatform.Marketing.Domain.Contacts;

/// <summary>
/// The audit record of one CSV contact import: who attested consent, to which text version, and what happened to the
/// file's rows. Written in the import's transaction; never changed afterwards except for the outcome counts, which are
/// known only after the rows were inserted.
/// </summary>
public sealed class ContactImport
{
    /// <summary>Version of the consent text the creator attested; bump it whenever <see cref="ConsentText"/> changes.</summary>
    public const string CurrentConsentVersion = "contacts-import-v1";

    public const string ConsentText =
        "I confirm that everyone in this file agreed to receive emails from me, and that I can show it if asked.";

    public const int MaxFileNameLength = 255;

    private ContactImport()
    {
    }

    public static ContactImport Start(
        int creatorId,
        int userId,
        string fileName,
        int rowCount,
        int duplicateCount,
        int unsubscribedCount,
        int invalidCount,
        DateTimeOffset now)
    {
        if (rowCount < 0 || duplicateCount < 0 || unsubscribedCount < 0 || invalidCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rowCount), "Counts cannot be negative.");

        var name = string.IsNullOrWhiteSpace(fileName) ? "contacts.csv" : fileName.Trim();

        return new ContactImport
        {
            PublicId = Guid.NewGuid(),
            CreatorId = creatorId,
            UserId = userId,
            FileName = name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name,
            RowCount = rowCount,
            DuplicateCount = duplicateCount,
            UnsubscribedCount = unsubscribedCount,
            InvalidCount = invalidCount,
            ConsentVersion = CurrentConsentVersion,
            ConsentAttestedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>The outcome once the rows are in: the contacts actually created, and the valid emails that were
    /// contacts already (including any a concurrent sign-up created first).</summary>
    public void RecordOutcome(int importedCount, int existingCount)
    {
        if (importedCount < 0 || existingCount < 0)
            throw new ArgumentOutOfRangeException(nameof(importedCount), "Counts cannot be negative.");

        ImportedCount = importedCount;
        ExistingCount = existingCount;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    /// <summary>The user who attested consent (the workspace owner who ran the import).</summary>
    public int UserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    /// <summary>Data rows read from the file (header excluded).</summary>
    public int RowCount { get; private set; }

    public int ImportedCount { get; private set; }

    public int ExistingCount { get; private set; }

    public int DuplicateCount { get; private set; }

    public int UnsubscribedCount { get; private set; }

    public int InvalidCount { get; private set; }

    public string ConsentVersion { get; private set; } = string.Empty;

    public DateTimeOffset ConsentAttestedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
