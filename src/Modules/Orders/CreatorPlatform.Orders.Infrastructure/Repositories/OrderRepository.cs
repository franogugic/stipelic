using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly CreatorPlatformDbContext _context;

    public OrderRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _context.Set<Order>().AddAsync(order, ct);
    }

    public async Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == stripeCheckoutSessionId, ct);
    }

    public async Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
    {
        // Load without AsNoTracking so EF tracks the entity for update; PostgreSQL FOR UPDATE is handled at DB level via the transaction
        return await _context
            .Set<Order>()
            .FromSqlInterpolated($"""
                SELECT * FROM orders.orders
                WHERE "StripeCheckoutSessionId" = {stripeCheckoutSessionId}
                LIMIT 1
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == stripePaymentIntentId, ct);
    }

    public async Task<List<OrderDto>> GetByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId
            orderby o.CreatedAt descending
            select new OrderDto(
                o.PublicId,
                o.Email,
                o.Name,
                p.Name,
                o.AmountCents,
                o.Currency.ToString(),
                o.Status.ToString(),
                o.CreatedAt,
                o.PaidAt)
        ).ToListAsync(ct);
    }

    public async Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        var summary = await (
            from o in _context.Set<Order>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId
            group o by c.DefaultCurrency into g
            select new
            {
                Currency = g.Key,
                PaidOrderCount = g.Count(o => o.Status == OrderStatus.Paid),
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0)
            }
        ).FirstOrDefaultAsync(ct);

        return summary is null
            ? new OrderSummaryDto(0, 0, null)
            : new OrderSummaryDto(summary.PaidOrderCount, summary.TotalPaidAmountCents, summary.Currency.ToString());
    }
}
