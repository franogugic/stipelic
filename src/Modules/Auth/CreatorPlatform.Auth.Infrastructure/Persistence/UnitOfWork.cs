using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPlatform.Auth.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private const string UsersEmailIndexName = "IX_users_Email";

    private readonly CreatorPlatformDbContext _context;

    public UnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueEmailViolation(exception))
        {
            throw new UserAlreadyExistsException();
        }
    }

    private static bool IsUniqueEmailViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
               postgresException.ConstraintName == UsersEmailIndexName;
    }
}
