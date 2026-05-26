using CreatorPlatform.Auth.Domain.Sessions;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken ct);
}
