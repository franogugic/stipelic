using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public sealed class EmailChangeTokenRepository : IEmailChangeTokenRepository
{
    private readonly CreatorPlatformDbContext _context;

    public EmailChangeTokenRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailChangeToken token, CancellationToken ct)
    {
        await _context.Set<EmailChangeToken>().AddAsync(token, ct);
    }

    public async Task<EmailChangeToken?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken ct)
    {
        return await _context.Set<EmailChangeToken>()
            .FromSqlInterpolated($"""
                SELECT * FROM auth.email_change_tokens
                WHERE "TokenHash" = {tokenHash}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<EmailChangeToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct)
    {
        return await _context.Set<EmailChangeToken>()
            .Where(token => token.UserId == userId && token.UsedAt == null)
            .ToListAsync(ct);
    }

    public async Task<EmailChangeToken?> GetPendingByUserIdAsync(int userId, DateTimeOffset now, CancellationToken ct)
    {
        return await _context.Set<EmailChangeToken>()
            .AsNoTracking()
            .Where(token => token.UserId == userId && token.UsedAt == null && token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
