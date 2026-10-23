using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class EmailCaptureService : IEmailCaptureService
{
    private const int DefaultCapturesLimit = 20;
    private const int MaxCapturesLimit = 100;

    private const string MaxContactsLimitKey = "max_contacts";
    private const string SignUpsClosedMessage = "Sign-ups are temporarily closed.";

    private readonly IEmailCaptureRepository _repository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICreatorUsageService _usageService;
    private readonly IAnalyticsUnitOfWork _unitOfWork;
    private readonly ILeadMagnetDelivery _leadMagnetDelivery;

    public EmailCaptureService(
        IEmailCaptureRepository repository,
        ICreatorContextProvider creatorContextProvider,
        ICreatorUsageService usageService,
        IAnalyticsUnitOfWork unitOfWork,
        ILeadMagnetDelivery leadMagnetDelivery)
    {
        _repository = repository;
        _creatorContextProvider = creatorContextProvider;
        _usageService = usageService;
        _unitOfWork = unitOfWork;
        _leadMagnetDelivery = leadMagnetDelivery;
    }

    public async Task CaptureAsync(int landingPageId, int? productId, int creatorId, string email, CancellationToken ct)
    {
        var limit = await _creatorContextProvider.GetActivePlanLimitAsync(creatorId, MaxContactsLimitKey, ct);
        if (limit is null)
            throw new ConflictException(SignUpsClosedMessage);

        var capture = new EmailCapture
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            ProductId = productId,
            Email = email.Trim().ToLowerInvariant(),
            CapturedAt = DateTimeOffset.UtcNow
        };

        // One transaction, so a sign-up rejected by the limit leaves no capture, summary or email behind.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Already captured on this page: the contact bookkeeping doesn't change, but the document may be re-sent.
            if (await _repository.AddAsync(capture, ct))
                await CountNewCaptureAsync(capture, creatorId, landingPageId, limit.Value, ct);

            // Unsubscribed emails too: they asked for this document (transactional), they just get no marketing.
            await _leadMagnetDelivery.DeliverAsync(creatorId, landingPageId, capture.Email, capture.CapturedAt, ct);
        }, ct);
    }

    /// <summary>Takes a contact place for a new contact; throws <see cref="ConflictException"/> at the limit.</summary>
    private async Task CountNewCaptureAsync(EmailCapture capture, int creatorId, int landingPageId, int limit, CancellationToken ct)
    {
        // max_contacts counts contacts, not captures: a known contact signing up on another page takes no
        // new slot and is never blocked by the limit.
        var isNewContact = await _repository.UpsertContactSummaryAsync(
            creatorId, landingPageId, capture.Email, capture.CapturedAt, ct);
        if (!isNewContact)
            return;

        // An unsubscribed email that signs up again stays unsubscribed, and unsubscribed contacts take no place
        // (F5.0): it is a contact again, but not counted and never blocked by the limit. Under the contact lock,
        // so a concurrent unsubscribe or delete of the same email can't miss this sign-up (or the other way round).
        await _unitOfWork.AcquireContactLockAsync(creatorId, capture.Email, ct);
        if (await _repository.IsUnsubscribedAsync(creatorId, capture.Email, ct))
            return;

        // TryConsumeAsync is a conditional atomic upsert, so parallel sign-ups can't overshoot the limit.
        // Throwing rolls back the capture and summary inserted above.
        var consumed = await _usageService.TryConsumeAsync(
            creatorId, MaxContactsLimitKey, 1, limit, UsagePeriod.AllTime, ct);
        if (!consumed)
            throw new ConflictException(SignUpsClosedMessage);
    }

    public Task<CapturesByPeriodRow> GetCaptureCountsByPeriodAsync(int landingPageId, StatsPeriods periods, CancellationToken ct) =>
        _repository.GetCaptureCountsByPeriodAsync(landingPageId, periods, ct);

    public Task<Dictionary<int, int>> GetCaptureCountsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct) =>
        _repository.GetCaptureCountsAsync(landingPageIds, ct);

    public async Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, int limit, CancellationToken ct)
    {
        var clampedLimit = limit <= 0 ? DefaultCapturesLimit : Math.Min(limit, MaxCapturesLimit);
        var captures = await _repository.ListNewestByLandingPageIdAsync(landingPageId, clampedLimit, ct);

        return captures
            .Select(c => new EmailCaptureResponseDto
            {
                Email = c.Email,
                CapturedAt = c.CapturedAt,
                Unsubscribed = c.Unsubscribed
            })
            .ToList();
    }
}
