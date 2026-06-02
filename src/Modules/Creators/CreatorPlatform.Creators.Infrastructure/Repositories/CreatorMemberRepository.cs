using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorMemberRepository : ICreatorMemberRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorMemberRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CreatorMember member, CancellationToken ct)
    {
        await _context.Set<CreatorMember>().AddAsync(member, ct);
    }
}
