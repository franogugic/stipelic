using CreatorPlatform.Marketing.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class OpenTrackingService : IOpenTrackingService
{
    private readonly IOpenTrackingTokenService _tokenService;
    private readonly ICampaignRecipientRepository _recipientRepository;
    private readonly ILogger<OpenTrackingService> _logger;

    public OpenTrackingService(
        IOpenTrackingTokenService tokenService,
        ICampaignRecipientRepository recipientRepository,
        ILogger<OpenTrackingService> logger)
    {
        _tokenService = tokenService;
        _recipientRepository = recipientRepository;
        _logger = logger;
    }

    public async Task RecordOpenAsync(string token, CancellationToken ct)
    {
        var recipientId = _tokenService.TryParse(token);
        if (recipientId is null)
            return;

        try
        {
            await _recipientRepository.RecordFirstOpenAsync(recipientId.Value, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to record campaign open for recipient {RecipientId}.", recipientId.Value);
        }
    }
}
