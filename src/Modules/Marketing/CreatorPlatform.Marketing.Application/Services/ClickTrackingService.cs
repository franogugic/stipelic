using CreatorPlatform.Marketing.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class ClickTrackingService : IClickTrackingService
{
    private readonly IClickTrackingTokenService _tokenService;
    private readonly ICampaignLinkRepository _linkRepository;
    private readonly ILogger<ClickTrackingService> _logger;

    public ClickTrackingService(
        IClickTrackingTokenService tokenService,
        ICampaignLinkRepository linkRepository,
        ILogger<ClickTrackingService> logger)
    {
        _tokenService = tokenService;
        _linkRepository = linkRepository;
        _logger = logger;
    }

    public async Task<string?> ResolveClickAsync(string token, CancellationToken ct)
    {
        if (_tokenService.TryParse(token) is not { } payload)
            return null;

        var target = await _linkRepository.GetTargetUrlAsync(payload.RecipientId, payload.LinkId, ct);
        if (target is null)
            return null;

        try
        {
            await _linkRepository.RecordClickAsync(payload.RecipientId, payload.LinkId, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception,
                "Failed to record a click on link {LinkId} for recipient {RecipientId}; redirecting anyway.",
                payload.LinkId, payload.RecipientId);
        }

        return target;
    }
}
