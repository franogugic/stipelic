using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Workspaces;

/// <summary>
/// Orders in which no money moved (Pending, Failed) are deleted; the erasure has already expired their Checkout sessions.
/// Paid and refunded orders are the platform's accounting record of the sale and its fee (Croatian Accounting Act:
/// accounting documents are kept for at least 11 years; GDPR Art. 6(1)(c), 17(3)(b)): they keep the amounts, fee,
/// refunds, currency, dates, Stripe ids, product, the buyer's email and the buyer's country, and lose the buyer's name
/// and the landing page. Erasing more later stays possible; what is erased cannot be restored. The owner's accountant
/// confirms the kept fields.
/// </summary>
public sealed class OrdersWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Orders;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM orders.orders WHERE "CreatorId" = {creatorId} AND "Status" IN ('Pending', 'Failed')
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "Name" = NULL, "LandingPageId" = NULL
            WHERE "CreatorId" = {creatorId} AND ("Name" IS NOT NULL OR "LandingPageId" IS NOT NULL)
            """, ct);
    }
}
