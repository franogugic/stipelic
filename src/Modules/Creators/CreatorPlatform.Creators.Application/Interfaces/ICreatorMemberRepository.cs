using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorMemberRepository
{
    Task AddAsync(CreatorMember member, CancellationToken ct);
}
