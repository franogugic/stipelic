using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Roles;
using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    public Dictionary<string, User> UsersByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(UsersByEmail.ContainsKey(email));

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(UsersByEmail.GetValueOrDefault(email));

    public Task<User?> GetByIdAsync(int id, CancellationToken ct)
        => Task.FromResult(UsersByEmail.Values.FirstOrDefault(u => u.Id == id));

    public Task<UserWithRoles?> GetByIdWithRolesAsync(int id, CancellationToken ct)
        => Task.FromResult<UserWithRoles?>(null);

    public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
}

public sealed class FakeTokenGenerator : ITokenGenerator
{
    public string NextToken { get; set; } = "raw-token";

    public string GenerateToken() => NextToken;
}

public sealed class FakeTokenHasher : ITokenHasher
{
    public string Hash(string token) => $"hash:{token}";
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

public sealed class FakeUserRoleRepository : IUserRoleRepository
{
    public Task AddAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakeEmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    public Task AddAsync(EmailVerificationToken token, CancellationToken ct) => Task.CompletedTask;

    public Task<EmailVerificationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
        => Task.FromResult<EmailVerificationToken?>(null);

    public Task<IReadOnlyList<EmailVerificationToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<EmailVerificationToken>>([]);

    public Task<EmailVerificationToken?> GetLatestByUserIdAsync(int userId, CancellationToken ct)
        => Task.FromResult<EmailVerificationToken?>(null);
}

public sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Tokens { get; } = [];

    public Task AddAsync(PasswordResetToken token, CancellationToken ct)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
        => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<PasswordResetToken>> GetUnusedByUserIdAsync(int userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PasswordResetToken>>(
            Tokens.Where(t => t.UserId == userId && t.UsedAt is null).ToList());
}

public sealed class FakeUserSessionRepository : IUserSessionRepository
{
    public List<UserSession> Sessions { get; } = [];

    public Task AddAsync(UserSession session, CancellationToken ct)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetValidByTokenHashAsync(string sessionTokenHash, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult(Sessions.FirstOrDefault(s =>
            s.SessionTokenHash == sessionTokenHash && s.RevokedAt is null && s.ExpiresAt > now));

    public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(int userId, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<UserSession>>(
            Sessions.Where(s => s.UserId == userId && s.RevokedAt is null && s.ExpiresAt > now).ToList());
}
