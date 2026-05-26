using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly CreatorPlatformDbContext _context;

    public UserRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }
    
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
    {
        return await _context
            .Set<User>()
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        return await _context
            .Set<User>()
            .FirstOrDefaultAsync(user => user.Email == email, ct);
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context
            .Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await _context.Set<User>().AddAsync(user, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
