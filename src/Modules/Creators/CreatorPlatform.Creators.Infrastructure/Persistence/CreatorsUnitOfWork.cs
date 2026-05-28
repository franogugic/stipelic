using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPlatform.Creators.Infrastructure.Persistence;

public sealed class CreatorsUnitOfWork : ICreatorsUnitOfWork
{
    private const string CreatorsOwnerUserIdIndexName = "IX_creators_OwnerUserId";
    private const string CreatorsSlugIndexName = "IX_creators_Slug";

    private readonly CreatorPlatformDbContext _context;

    public CreatorsUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (TryMapUniqueViolation(exception, out var message))
        {
            throw new ConflictException(message);
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await operation();

        await transaction.CommitAsync(ct);
    }

    private static bool TryMapUniqueViolation(DbUpdateException exception, out string message)
    {
        message = string.Empty;

        if (exception.InnerException is not PostgresException postgresException ||
            postgresException.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }

        message = postgresException.ConstraintName switch
        {
            CreatorsOwnerUserIdIndexName => "You already have a creator workspace.",
            CreatorsSlugIndexName => "This creator URL is already taken.",
            _ => "Creator data conflicts with an existing record."
        };

        return true;
    }
}
