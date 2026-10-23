using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>
/// Unsubscribes (page link and one-click alike). <c>max_contacts</c> counts contacts that are not unsubscribed (F5.0),
/// so the counter equals the creator's contact rows without an unsubscribe: an unsubscribe of a contact gives one
/// place back, exactly once — only the call that inserts the unsubscribe refunds, and under the contact lock, so it
/// can't race a delete of the same contact (which refunds only contacts that are not unsubscribed).
/// </summary>
public sealed class UnsubscribeService : IUnsubscribeService
{
    private const string MaxContactsLimitKey = "max_contacts";

    private readonly IUnsubscribeRepository _unsubscribeRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly ICreatorUsageService _usageService;
    private readonly IMarketingUnitOfWork _unitOfWork;

    public UnsubscribeService(
        IUnsubscribeRepository unsubscribeRepository,
        IContactsRepository contactsRepository,
        ICreatorUsageService usageService,
        IMarketingUnitOfWork unitOfWork)
    {
        _unsubscribeRepository = unsubscribeRepository;
        _contactsRepository = contactsRepository;
        _usageService = usageService;
        _unitOfWork = unitOfWork;
    }

    public async Task UnsubscribeAsync(int creatorId, string email, UnsubscribeSource source, CancellationToken ct)
    {
        var unsubscribe = Unsubscribe.Create(creatorId, email, source, DateTimeOffset.UtcNow);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _unitOfWork.AcquireContactLockAsync(creatorId, unsubscribe.Email, ct);

            var inserted = await _unsubscribeRepository.AddIfNotExistsAsync(unsubscribe, ct);
            if (!inserted || !await _contactsRepository.ExistsAsync(creatorId, unsubscribe.Email, ct))
                return;

            // AllTime counters have one fixed period row, so asOf selects nothing here.
            await _usageService.RefundAsync(
                creatorId, MaxContactsLimitKey, 1, UsagePeriod.AllTime, DateTimeOffset.UtcNow, ct);
        }, ct);
    }
}
