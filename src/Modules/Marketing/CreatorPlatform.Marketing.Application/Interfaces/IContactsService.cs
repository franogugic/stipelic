using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IContactsService
{
    Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? afterEmail, int limit,
        CancellationToken ct);

    Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct);
}
