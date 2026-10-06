using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class AnalyticsContextProvider : IAnalyticsContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public AnalyticsContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int> CountContactsAsync(int productId, CancellationToken ct)
    {
        // The same union as the Product campaign audience: captures tagged with the product directly, and captures on
        // any landing page that sells it. UNION dedupes both sides, so an email counts once.
        var rows = await _context.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM (
                SELECT ec."Email" FROM analytics.email_captures ec WHERE ec."ProductId" = {productId}
                UNION
                SELECT ec."Email"
                FROM analytics.email_captures ec
                JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
                WHERE lp."ProductId" = {productId}
            ) AS combined
            """)
            .ToListAsync(ct);

        return rows.Single();
    }
}
