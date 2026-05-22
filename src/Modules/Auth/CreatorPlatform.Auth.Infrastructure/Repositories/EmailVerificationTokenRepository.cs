using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly CreatorPlatformDbContext _context;

    public EmailVerificationTokenRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(EmailVerificationToken emailVerificationToken, CancellationToken ct)
    {
        await _context.Set<EmailVerificationToken>().AddAsync(emailVerificationToken, ct);
    }

    public async Task<EmailVerificationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        return await _context.Set<EmailVerificationToken>()
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
    }

    public async Task<IReadOnlyList<EmailVerificationToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct)
    {
        return await _context.Set<EmailVerificationToken>()
            .Where(token => token.UserId == userId && token.UsedAt == null)
            .ToListAsync(ct);
    }

    public async Task<EmailVerificationToken?> GetLatestByUserIdAsync(int userId, CancellationToken ct)
    {
        return await _context.Set<EmailVerificationToken>()
            .Where(token => token.UserId == userId)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
