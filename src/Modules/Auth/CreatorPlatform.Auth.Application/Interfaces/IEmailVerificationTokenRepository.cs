using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token, CancellationToken ct);
}