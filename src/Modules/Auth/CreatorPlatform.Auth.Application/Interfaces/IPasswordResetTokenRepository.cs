using CreatorPlatform.Auth.Domain.Tokens;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct);
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<PasswordResetToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct);
}
