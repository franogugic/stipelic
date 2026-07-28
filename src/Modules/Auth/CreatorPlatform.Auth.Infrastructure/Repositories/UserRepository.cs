using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Roles;
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

    public async Task<UserWithRoles?> GetByIdWithRolesAsync(int id, CancellationToken ct)
    {
        var rows = await (
            from u in _context.Set<User>().AsNoTracking()
            where u.Id == id
            join ur in _context.Set<UserRole>().AsNoTracking() on u.Id equals ur.UserId into userRoles
            from ur in userRoles.DefaultIfEmpty()
            join r in _context.Set<Role>().AsNoTracking() on ur.RoleId equals r.Id into roles
            from r in roles.DefaultIfEmpty()
            select new { User = u, RoleName = (string?)r.Name }
        ).ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        var roleNames = rows
            .Where(row => row.RoleName is not null)
            .Select(row => row.RoleName!)
            .Distinct()
            .ToList();

        return new UserWithRoles(rows[0].User, roleNames);
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
