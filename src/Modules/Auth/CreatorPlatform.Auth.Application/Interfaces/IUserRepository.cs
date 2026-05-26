using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
