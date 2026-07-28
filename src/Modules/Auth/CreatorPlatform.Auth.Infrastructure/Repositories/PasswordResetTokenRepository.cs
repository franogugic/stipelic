using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly CreatorPlatformDbContext _context;

    public PasswordResetTokenRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PasswordResetToken token, CancellationToken ct)
    {
        await _context.Set<PasswordResetToken>().AddAsync(token, ct);
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        return await _context.Set<PasswordResetToken>()
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
    }

    public async Task<IReadOnlyList<PasswordResetToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct)
    {
        return await _context.Set<PasswordResetToken>()
            .Where(token => token.UserId == userId && token.UsedAt == null)
            .ToListAsync(ct);
    }
}
