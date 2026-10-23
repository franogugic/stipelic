namespace CreatorPlatform.Marketing.Application.Dtos;

/// <param name="Sources">Legacy flat label: up to three source titles, alphabetical, comma-separated. Kept for
/// the current frontend; new code should render <paramref name="SourceList"/>.</param>
/// <param name="SourceList">Every source landing page (archived included), in first-capture order.</param>
/// <param name="ImportedAt">When a CSV import created the contact (its first source, "Import"); null for contacts that
/// signed up on a page.</param>
public sealed record ContactDto(
    string Email,
    DateTimeOffset FirstCapturedAt,
    int SourcesCount,
    string Sources,
    bool IsUnsubscribed,
    List<ContactSourceDto> SourceList,
    DateTimeOffset? ImportedAt);

public sealed record ContactSourceDto(Guid LandingPagePublicId, string Title);

public sealed record ContactsPageDto(List<ContactDto> Contacts, bool HasMore);
