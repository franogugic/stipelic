using CreatorPlatform.Auth.Domain.Sessions;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken ct);

    Task<UserSession?> GetValidByTokenHashAsync(string sessionTokenHash, DateTimeOffset now, CancellationToken ct);

    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken ct);
}
