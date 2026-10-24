using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Orders.Infrastructure.Maintenance;

/// <summary>
/// Marks checkouts that never completed as <see cref="OrderStatus.Failed"/>: orders still
/// <see cref="OrderStatus.Pending"/> <see cref="MaintenanceOptions.StalePendingOrderHours"/> after they were created. By
/// then their Stripe Checkout session has expired (24 h by default; the default age adds an hour), so the buyer can no longer pay it. The webhook for
/// <c>checkout.session.expired</c> does the same sooner when Stripe delivers it; this job catches the rest.
///
/// Nothing is lost if a payment does arrive later (e.g. a delayed webhook retry): <c>checkout.session.completed</c> pays any
/// order that is not already Paid or Refunded, Failed included. An order paid while this job runs is never overwritten:
/// the UPDATE re-checks "still Pending" on the row it is about to change.
/// </summary>
public sealed class FailStalePendingOrdersTask(
    CreatorPlatformDbContext db,
    IOptions<MaintenanceOptions> options) : IMaintenanceTask
{
    public string Name => "stale-pending-orders";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = options.Value;
        var startedBefore = now.AddHours(-settings.StalePendingOrderHours);
        var orders = db.Set<Order>();
        var total = 0;

        while (true)
        {
            var batch = orders
                .Where(order => order.Status == OrderStatus.Pending && order.CreatedAt < startedBefore)
                .OrderBy(order => order.CreatedAt)
                .Take(settings.BatchSize)
                .Select(order => order.Id);

            // "Status = Pending" again on the target row: if a webhook paid the order after the batch was read, Postgres
            // re-evaluates this condition on the paid row once the webhook's lock is released, and skips it.
            var failed = await orders
                .Where(order => batch.Contains(order.Id) && order.Status == OrderStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(order => order.Status, OrderStatus.Failed)
                    .SetProperty(order => order.UpdatedAt, now), ct);

            total += failed;
            if (failed < settings.BatchSize)
                return total;
        }
    }
}
