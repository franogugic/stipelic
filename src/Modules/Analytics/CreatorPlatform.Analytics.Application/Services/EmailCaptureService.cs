using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class EmailCaptureService : IEmailCaptureService
{
    private const string MaxContactsLimitKey = "max_contacts";

    private readonly IEmailCaptureRepository _repository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICreatorUsageService _usageService;
    private readonly IAnalyticsUnitOfWork _unitOfWork;

    public EmailCaptureService(
        IEmailCaptureRepository repository,
        ICreatorContextProvider creatorContextProvider,
        ICreatorUsageService usageService,
        IAnalyticsUnitOfWork unitOfWork)
    {
        _repository = repository;
        _creatorContextProvider = creatorContextProvider;
        _usageService = usageService;
        _unitOfWork = unitOfWork;
    }

    public async Task CaptureAsync(int landingPageId, int? productId, int creatorId, string email, CancellationToken ct)
    {
        var limit = await _creatorContextProvider.GetActivePlanLimitAsync(creatorId, MaxContactsLimitKey, ct);
        if (limit is null)
            throw new ConflictException("Sign-ups are temporarily closed.");

        if (limit >= 0)
        {
            var usedSoFar = await _usageService.GetUsedAsync(creatorId, MaxContactsLimitKey, UsagePeriod.AllTime, ct);
            if (usedSoFar >= limit)
                throw new ConflictException("Sign-ups are temporarily closed.");
        }

        var capture = new EmailCapture
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            ProductId = productId,
            Email = email.Trim().ToLowerInvariant(),
            CapturedAt = DateTimeOffset.UtcNow
        };

        var inserted = await _repository.AddAsync(capture, ct);
        if (inserted)
        {
            await _usageService.TryConsumeAsync(creatorId, MaxContactsLimitKey, 1, limit.Value, UsagePeriod.AllTime, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct) =>
        _repository.GetCaptureCountAsync(landingPageId, ct);

    public async Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, CancellationToken ct)
    {
        var captures = await _repository.ListByLandingPageIdAsync(landingPageId, ct);

        return captures
            .Select(c => new EmailCaptureResponseDto
            {
                Email = c.Email,
                CapturedAt = c.CapturedAt
            })
            .ToList();
    }
}
