namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record ContactDto(
    string Email,
    DateTimeOffset FirstCapturedAt,
    int SourcesCount,
    string Sources,
    bool IsUnsubscribed);

public sealed record ContactsPageDto(List<ContactDto> Contacts, bool HasMore);
