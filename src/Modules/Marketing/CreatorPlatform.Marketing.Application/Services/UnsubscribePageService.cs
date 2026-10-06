using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class UnsubscribePageService : IUnsubscribePageService
{
    public const string InvalidLinkCode = "unsubscribe_link_invalid";
    private const string InvalidLinkMessage = "This unsubscribe link is invalid or has expired.";

    private readonly IUnsubscribeTokenService _tokenService;
    private readonly IUnsubscribeRepository _unsubscribeRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;

    public UnsubscribePageService(
        IUnsubscribeTokenService tokenService,
        IUnsubscribeRepository unsubscribeRepository,
        ICreatorContextProvider creatorContextProvider)
    {
        _tokenService = tokenService;
        _unsubscribeRepository = unsubscribeRepository;
        _creatorContextProvider = creatorContextProvider;
    }

    public async Task<UnsubscribePageDto> GetInfoAsync(string token, CancellationToken ct)
    {
        var (payload, creator) = await ResolveAsync(token, ct);
        var alreadyUnsubscribed = await _unsubscribeRepository.ExistsAsync(payload.CreatorId, payload.Email, ct);
        return new UnsubscribePageDto(creator, alreadyUnsubscribed);
    }

    public async Task<UnsubscribePageDto> ConfirmAsync(string token, CancellationToken ct)
    {
        var (payload, creator) = await ResolveAsync(token, ct);
        await _unsubscribeRepository.AddIfNotExistsAsync(
            Unsubscribe.Create(payload.CreatorId, payload.Email, UnsubscribeSource.Link, DateTimeOffset.UtcNow), ct);
        return new UnsubscribePageDto(creator, AlreadyUnsubscribed: true);
    }

    private async Task<(UnsubscribeTokenPayload Payload, UnsubscribePageCreatorDto Creator)> ResolveAsync(
        string token, CancellationToken ct)
    {
        var payload = _tokenService.TryParse(token)
            ?? throw new NotFoundException(InvalidLinkMessage, InvalidLinkCode);

        // A deleted (Disabled) workspace resolves to null: its links stop working along with it.
        var context = await _creatorContextProvider.GetByCreatorIdAsync(payload.CreatorId, ct)
            ?? throw new NotFoundException(InvalidLinkMessage, InvalidLinkCode);

        return (payload, new UnsubscribePageCreatorDto(
            string.IsNullOrWhiteSpace(context.BrandName) ? context.Name : context.BrandName,
            context.PrimaryColor,
            context.LogoUrl,
            context.SupportEmail));
    }
}
