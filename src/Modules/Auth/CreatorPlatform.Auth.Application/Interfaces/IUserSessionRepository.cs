using CreatorPlatform.Auth.Domain.Sessions;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken ct);

    Task<UserSession?> GetValidByTokenHashAsync(string sessionTokenHash, DateTimeOffset now, CancellationToken ct);

    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Every currently-active (not yet revoked, not yet expired) session for a user — tracked,
    /// so the caller can revoke them all in one go (e.g. "sign out everywhere" on password reset).</summary>
    Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(int userId, DateTimeOffset now, CancellationToken ct);
}
