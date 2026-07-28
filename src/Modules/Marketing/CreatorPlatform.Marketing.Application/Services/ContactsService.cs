using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class ContactsService : IContactsService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IContactsRepository _contactsRepository;

    public ContactsService(ICreatorContextProvider creatorContextProvider, IContactsRepository contactsRepository)
    {
        _creatorContextProvider = creatorContextProvider;
        _contactsRepository = contactsRepository;
    }

    public async Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, string? afterEmail, int limit, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _contactsRepository.SearchAsync(context.CreatorId, search, afterEmail, clampedLimit, ct);
        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;

        var contacts = page
            .Select(r => new ContactDto(r.Email, r.FirstCapturedAt, r.SourcesCount, r.Sources, r.IsUnsubscribed))
            .ToList();

        return new ContactsPageDto(contacts, hasMore);
    }
}
