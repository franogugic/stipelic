using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class CampaignRecipientRepository : ICampaignRecipientRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CampaignRecipientRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<CampaignRecipient> recipients, CancellationToken ct)
    {
        await _context.Set<CampaignRecipient>().AddRangeAsync(recipients, ct);
    }
}
