using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Roles;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Auth.Infrastructure.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly CreatorPlatformDbContext _context;

    public UserRoleRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(UserRole userRole, CancellationToken ct)
    {
        await _context.Set<UserRole>().AddAsync(userRole, ct);
    }
}