using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly CreatorPlatformDbContext _context;

    public UserSessionRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserSession session, CancellationToken ct)
    {
        await _context.Set<UserSession>().AddAsync(session, ct);
    }

    public async Task<UserSession?> GetValidByTokenHashAsync(string sessionTokenHash, DateTimeOffset now, CancellationToken ct)
    {
        return await _context.Set<UserSession>()
            .AsNoTracking()
            .FirstOrDefaultAsync(session =>
                    session.SessionTokenHash == sessionTokenHash &&
                    session.RevokedAt == null &&
                    session.ExpiresAt > now,
                ct);
    }
}
