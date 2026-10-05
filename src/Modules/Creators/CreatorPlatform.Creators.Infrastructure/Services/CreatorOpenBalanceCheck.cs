using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Services;

public sealed class CreatorOpenBalanceCheck : ICreatorOpenBalanceCheck
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorOpenBalanceCheck(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasOpenBalanceAsync(int creatorId, CancellationToken ct)
    {
        // A Pending request has already been debited from the ledger, so the balance alone can read 0 while money
        // is still on its way to the creator — both conditions are needed.
        return (await _context.Database.SqlQuery<bool>($"""
            SELECT
                EXISTS (
                    SELECT 1 FROM payouts.ledger_entries
                    WHERE "CreatorId" = {creatorId}
                    GROUP BY "Currency"
                    HAVING SUM("AmountCents") <> 0)
                OR EXISTS (
                    SELECT 1 FROM payouts.payouts
                    WHERE "CreatorId" = {creatorId} AND "Status" = 'Pending') AS "Value"
            """).ToListAsync(ct)).Single();
    }
}
