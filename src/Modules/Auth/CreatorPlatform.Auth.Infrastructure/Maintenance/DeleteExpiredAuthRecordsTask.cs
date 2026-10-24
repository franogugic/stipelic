using System.Linq.Expressions;
using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Auth.Infrastructure.Maintenance;

/// <summary>
/// Deletes sessions and one-time tokens <see cref="MaintenanceOptions.ExpiredAuthRecordRetentionDays"/> after they
/// expired. A revoked session goes the same time after its revocation (signing out, a password reset or an email change
/// revoke sessions long before they expire). None of these rows can authenticate anything any more; the delay only keeps
/// them for a while for support questions.
/// </summary>
public sealed class DeleteExpiredAuthRecordsTask(
    CreatorPlatformDbContext db,
    IOptions<MaintenanceOptions> options) : IMaintenanceTask
{
    public string Name => "expired-auth-records";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var deadBefore = now.AddDays(-options.Value.ExpiredAuthRecordRetentionDays);

        return await DeleteInBatchesAsync<UserSession>(
                   session => session.ExpiresAt < deadBefore || session.RevokedAt < deadBefore, session => session.Id, ct)
               + await DeleteInBatchesAsync<EmailVerificationToken>(token => token.ExpiresAt < deadBefore, token => token.Id, ct)
               + await DeleteInBatchesAsync<PasswordResetToken>(token => token.ExpiresAt < deadBefore, token => token.Id, ct)
               + await DeleteInBatchesAsync<EmailChangeToken>(token => token.ExpiresAt < deadBefore, token => token.Id, ct);
    }

    private async Task<int> DeleteInBatchesAsync<TEntity>(
        Expression<Func<TEntity, bool>> isDead,
        Expression<Func<TEntity, Guid>> id,
        CancellationToken ct)
        where TEntity : class
    {
        var batchSize = options.Value.BatchSize;
        var total = 0;

        while (true)
        {
            var deleted = await db.Set<TEntity>()
                .Where(isDead)
                .OrderBy(id)
                .Take(batchSize)
                .ExecuteDeleteAsync(ct);

            total += deleted;
            if (deleted < batchSize)
                return total;
        }
    }
}
