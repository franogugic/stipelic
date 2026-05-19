using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Shared.Infrastructure.Persistence;

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
}