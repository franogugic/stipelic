using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>
/// CSV contact import (F5.1). Synchronous: a real import completes or rolls back as a whole. Rows are checked like a
/// page sign-up; an unsubscribed email is never imported; an existing contact is not touched. The plan's contact limit
/// is all or nothing: the new contacts must all fit, and the counter grows by exactly the number inserted, in the same
/// transaction (a refused consume rolls the inserts back).
/// </summary>
public sealed class ContactImportService : IContactImportService
{
    public const int MaxRows = 10_000;
    public const int MaxInvalidListed = 100;
    public const string ContactLimitExceededCode = "CONTACT_LIMIT_EXCEEDED";

    /// <summary>The <c>details</c> of a CONTACT_LIMIT_EXCEEDED conflict: the new contacts and the places left.</summary>
    public sealed record ContactLimitDetails(int New, int Remaining);

    private const string MaxContactsLimitKey = "max_contacts";

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IContactCsvReader _csvReader;
    private readonly IContactImportRepository _importRepository;
    private readonly ICreatorUsageService _usageService;
    private readonly IMarketingUnitOfWork _unitOfWork;

    public ContactImportService(
        ICreatorContextProvider creatorContextProvider,
        IContactCsvReader csvReader,
        IContactImportRepository importRepository,
        ICreatorUsageService usageService,
        IMarketingUnitOfWork unitOfWork)
    {
        _creatorContextProvider = creatorContextProvider;
        _csvReader = csvReader;
        _importRepository = importRepository;
        _usageService = usageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ContactImportSummaryDto> ImportAsync(
        string slug, int ownerUserId, string fileName, Stream file, bool consentAttested, bool dryRun, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        if (!dryRun && !consentAttested)
            throw new BadRequestException(
                "Confirm that everyone in the file agreed to receive your emails before importing.",
                ContactImportErrors.ConsentRequired);

        var cells = await _csvReader.ReadEmailColumnAsync(file, MaxRows, ct);
        if (cells.Count == 0)
            throw new BadRequestException("The file has no rows.", ContactImportErrors.FileEmpty);

        var rows = CheckRows(cells);

        var limit = await _creatorContextProvider.GetActivePlanLimitAsync(context.CreatorId, MaxContactsLimitKey, ct)
            ?? throw new ConflictException("Imports are temporarily unavailable.");

        if (dryRun)
        {
            var preview = await ClassifyAsync(context.CreatorId, rows, ct);
            var used = await UsedAsync(context.CreatorId, ct);
            return Summary(true, rows, preview.New.Count, preview.Existing, preview.Unsubscribed, Remaining(limit, used + preview.New.Count));
        }

        ContactImportSummaryDto? summary = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Exclusive per creator: two imports can't overlap, and an unsubscribe (which takes this lock shared) can
            // neither slip in between the classification below and the inserts, nor miss a contact the import creates.
            await _unitOfWork.AcquireCreatorContactsLockAsync(context.CreatorId, exclusive: true, ct);

            var classified = await ClassifyAsync(context.CreatorId, rows, ct);
            var used = await UsedAsync(context.CreatorId, ct);
            if (limit >= 0 && used + classified.New.Count > limit)
            {
                var remaining = Math.Max(0, limit - used);
                throw new ConflictException(
                    $"This file has {classified.New.Count} new contact(s) and your plan has room for {remaining}. Nothing was imported.",
                    ContactLimitExceededCode,
                    new ContactLimitDetails(classified.New.Count, remaining));
            }

            var now = DateTimeOffset.UtcNow;
            var import = ContactImport.Start(
                context.CreatorId, ownerUserId, fileName, rows.Rows, rows.Duplicates, classified.Unsubscribed, rows.Invalid.Count, now);
            await _importRepository.AddAsync(import, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var inserted = await _importRepository.InsertContactsAsync(context.CreatorId, import.Id, classified.New, now, ct);
            // A concurrent sign-up can win an email between the classification and the insert: it is a contact
            // that already existed by the time this import ran.
            var existing = classified.Existing + (classified.New.Count - inserted);
            import.RecordOutcome(inserted, existing);
            await _unitOfWork.SaveChangesAsync(ct);

            if (inserted > 0 && !await _usageService.TryConsumeAsync(
                    context.CreatorId, MaxContactsLimitKey, inserted, limit, UsagePeriod.AllTime, ct))
            {
                // Only reachable if the counter moved since the check above (it can't, under the lock, unless the
                // counter disagrees with the contacts); refusing rolls every insert back.
                throw new ConflictException(
                    "Your plan's contact limit changed during the import. Nothing was imported.", ContactLimitExceededCode);
            }

            summary = Summary(false, rows, inserted, existing, classified.Unsubscribed, Remaining(limit, used + inserted));
        }, ct);

        return summary!;
    }

    private sealed record CheckedRows(int Rows, List<string> Distinct, int Duplicates, List<ContactImportInvalidRowDto> Invalid);

    private sealed record Classified(List<string> New, int Existing, int Unsubscribed);

    /// <summary>Validates and normalises every cell; the first occurrence of an email counts, later ones are
    /// duplicates.</summary>
    private static CheckedRows CheckRows(List<ContactCsvCell> cells)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<string>();
        var invalid = new List<ContactImportInvalidRowDto>();
        var duplicates = 0;

        foreach (var cell in cells)
        {
            var check = ContactEmailRules.Check(cell.Value);
            if (check.Normalized is null)
                invalid.Add(new ContactImportInvalidRowDto(cell.Row, check.Value, check.Reason!));
            else if (seen.Add(check.Normalized))
                distinct.Add(check.Normalized);
            else
                duplicates++;
        }

        return new CheckedRows(cells.Count, distinct, duplicates, invalid);
    }

    /// <summary>An unsubscribed email is reported as unsubscribed even when it is still a contact: either way it is
    /// not imported, and that is the more useful thing to tell the creator.</summary>
    private async Task<Classified> ClassifyAsync(int creatorId, CheckedRows rows, CancellationToken ct)
    {
        var (contacts, unsubscribed) = await _importRepository.ClassifyAsync(creatorId, rows.Distinct, ct);

        var unsubscribedCount = rows.Distinct.Count(unsubscribed.Contains);
        var existingCount = rows.Distinct.Count(email => !unsubscribed.Contains(email) && contacts.Contains(email));
        var newEmails = rows.Distinct.Where(email => !unsubscribed.Contains(email) && !contacts.Contains(email)).ToList();

        return new Classified(newEmails, existingCount, unsubscribedCount);
    }

    private Task<int> UsedAsync(int creatorId, CancellationToken ct) =>
        _usageService.GetUsedAsync(creatorId, MaxContactsLimitKey, UsagePeriod.AllTime, ct);

    private static int? Remaining(int limit, int usedAfter) => limit < 0 ? null : limit - usedAfter;

    private static ContactImportSummaryDto Summary(
        bool dryRun, CheckedRows rows, int newCount, int existing, int unsubscribed, int? remaining) =>
        new(
            dryRun,
            rows.Rows,
            rows.Rows - rows.Invalid.Count,
            newCount,
            existing,
            rows.Duplicates,
            unsubscribed,
            rows.Invalid.Count,
            rows.Invalid.Take(MaxInvalidListed).ToList(),
            remaining);
}
