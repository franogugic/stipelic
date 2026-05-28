using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorPlanRepository : ICreatorPlanRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorPlanRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<CreatorPlan>> ListActiveAsync(CancellationToken ct)
    {
        return await _context
            .Set<CreatorPlan>()
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<CreatorPlan?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await _context
            .Set<CreatorPlan>()
            .FirstOrDefaultAsync(plan => plan.Code == code, ct);
    }
}
