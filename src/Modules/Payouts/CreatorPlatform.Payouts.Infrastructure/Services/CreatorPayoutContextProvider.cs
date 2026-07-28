using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payouts.Infrastructure.Services;

public sealed class CreatorPayoutContextProvider : ICreatorPayoutContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorPayoutContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<CreatorPayoutContext?> GetByPublicIdAsync(Guid creatorPublicId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.PublicId == creatorPublicId)
            .Select(c => new CreatorPayoutContext(
                c.Id,
                c.PublicId,
                c.Name,
                c.Slug,
                c.Status,
                c.PayoutMode,
                c.DefaultCurrency,
                _context.Set<CreatorPayoutProfile>().Any(p => p.CreatorId == c.Id)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CreatorPayoutContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled)
            .Select(c => new CreatorPayoutContext(
                c.Id,
                c.PublicId,
                c.Name,
                c.Slug,
                c.Status,
                c.PayoutMode,
                c.DefaultCurrency,
                _context.Set<CreatorPayoutProfile>().Any(p => p.CreatorId == c.Id)))
            .FirstOrDefaultAsync(ct);
    }
}
