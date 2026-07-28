using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Media.Infrastructure.Repositories;

public sealed class BlobReferenceRepository : IBlobReferenceRepository
{
    private readonly CreatorPlatformDbContext _context;

    public BlobReferenceRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BlobReference reference, CancellationToken ct)
    {
        await _context.Set<BlobReference>().AddAsync(reference, ct);
    }

    public async Task<BlobReference?> GetPendingByCreatorAndUrlAsync(int creatorId, string blobUrl, CancellationToken ct)
    {
        return await _context.Set<BlobReference>()
            .FirstOrDefaultAsync(
                b => b.CreatorId == creatorId && b.BlobUrl == blobUrl && b.Status == BlobReferenceStatus.Pending,
                ct);
    }
}
