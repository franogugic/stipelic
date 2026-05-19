using CreatorPlatform.Auth.Domain.Roles;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUserRoleRepository
{
    Task AddAsync(UserRole userRole, CancellationToken ct);
}