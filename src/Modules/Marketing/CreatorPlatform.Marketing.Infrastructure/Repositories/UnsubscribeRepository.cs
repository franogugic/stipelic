using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class UnsubscribeRepository : IUnsubscribeRepository
{
    private readonly CreatorPlatformDbContext _context;

    public UnsubscribeRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddIfNotExistsAsync(Unsubscribe unsubscribe, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO marketing.unsubscribes ("CreatorId", "Email", "Source", "UnsubscribedAt")
             VALUES ({unsubscribe.CreatorId}, {unsubscribe.Email}, {unsubscribe.Source.ToString()}, {unsubscribe.UnsubscribedAt})
             ON CONFLICT ("CreatorId", "Email") DO NOTHING
             """,
            ct);
    }

    public async Task<bool> ExistsAsync(int creatorId, string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Set<Unsubscribe>()
            .AsNoTracking()
            .AnyAsync(u => u.CreatorId == creatorId && u.Email == normalizedEmail, ct);
    }
}
