using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IContactImportService
{
    /// <summary>Imports (or, with <paramref name="dryRun"/>, only checks) a contacts CSV for the owner's workspace. A
    /// real import needs <paramref name="consentAttested"/>, is all or nothing against the plan's contact limit, and
    /// completes or rolls back as a whole.</summary>
    Task<ContactImportSummaryDto> ImportAsync(
        string slug, int ownerUserId, string fileName, Stream file, bool consentAttested, bool dryRun, CancellationToken ct);
}
