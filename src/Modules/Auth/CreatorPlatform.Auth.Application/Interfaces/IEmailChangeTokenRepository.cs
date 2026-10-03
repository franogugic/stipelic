using CreatorPlatform.Auth.Domain.Tokens;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IEmailChangeTokenRepository
{
    Task AddAsync(EmailChangeToken token, CancellationToken ct);

    /// <summary>Loads the token and locks its row (<c>FOR UPDATE</c>) until the surrounding transaction ends, so
    /// two confirmations of the same link serialize and only the first can use it. Must run in a transaction.</summary>
    Task<EmailChangeToken?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken ct);

    /// <summary>The user's tokens that were neither used nor invalidated — tracked, to retire them.</summary>
    Task<IReadOnlyList<EmailChangeToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct);
}
