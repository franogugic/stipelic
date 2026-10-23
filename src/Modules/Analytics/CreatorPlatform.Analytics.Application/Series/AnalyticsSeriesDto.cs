namespace CreatorPlatform.Analytics.Application.Series;

/// <summary>A page's or a product's numbers for one period, in the creator's timezone: the totals, the chart buckets
/// and, except for "all", the period before.</summary>
/// <param name="Scope">"page" | "product" | "workspace".</param>
/// <param name="Period">"today" | "7d" | "15d" | "30d" | "2m" | "3m" | "6m" | "all".</param>
/// <param name="Step">"hour" | "day" | "week" | "month" — the bucket size.</param>
/// <param name="Timezone">The IANA zone the buckets and ranges are in (UTC when the creator has none).</param>
/// <param name="Currency">The creator's currency ("Eur" | "Usd").</param>
/// <param name="Since">When the page, product or workspace was created — the start of "all".</param>
/// <param name="Range">The period: "today" and the day periods end now; "all" starts at <paramref name="Since"/>.</param>
/// <param name="PreviousRange">The period before: yesterday up to the same local time for "today", the N days before
/// for the day periods, the N calendar months before for 2m / 3m / 6m; null for "all".</param>
/// <param name="Buckets">Oldest first, zero-filled. "today": every local hour so far; "7d"/"15d"/"30d": every local
/// day; "2m"/"3m": 7-day buckets and "6m": monthly buckets, anchored at the window's first day (the day after the same
/// date N months ago), the last one ending now; "all": the last 12 local months, or since the page/product's month when
/// that is shorter.</param>
/// <param name="PreviousBuckets">Yesterday's 24 hours, or the buckets of the window before; null for "all".</param>
/// <param name="BestMonth">"all" only: the strongest of <paramref name="Buckets"/> per metric (null when it is 0).</param>
/// <param name="Product">Product scope only: refunds in the period and the creator's revenue in the same period.</param>
/// <param name="Extras">Workspace and product scope (null for a page): refunds, customers and sign-ups in the period.</param>
public sealed record AnalyticsSeriesDto(
    string Scope,
    string Period,
    string Step,
    string Timezone,
    string Currency,
    DateTimeOffset Since,
    AnalyticsRangeDto Range,
    AnalyticsRangeDto? PreviousRange,
    AnalyticsTotalsDto Totals,
    AnalyticsTotalsDto? PreviousTotals,
    List<AnalyticsBucketDto> Buckets,
    List<AnalyticsBucketDto>? PreviousBuckets,
    AnalyticsBestMonthDto? BestMonth,
    AnalyticsProductExtrasDto? Product,
    AnalyticsExtrasDto? Extras);

public sealed record AnalyticsRangeDto(DateTimeOffset From, DateTimeOffset To);

/// <param name="Views">Page views (one per visitor per page per UTC day).</param>
/// <param name="Emails">Distinct emails collected (across the product's pages for a product). For the workspace: new
/// contacts, counted in the bucket their first capture falls in.</param>
/// <param name="Sales">Paid orders, by PaidAt (else CreatedAt).</param>
/// <param name="RevenueCents">Their AmountCents.</param>
/// <param name="Downloads">Download requests through personal links, on the day they happen (repeats included; none before
/// the R6.2 release): the product's links for a product, the links that came from the page for a page, every link for
/// the workspace.</param>
public sealed record AnalyticsTotalsDto(long Views, long Emails, long Sales, long RevenueCents, long Downloads);

/// <param name="Start">The bucket's local start, "yyyy-MM-ddTHH:mm" in <see cref="AnalyticsSeriesDto.Timezone"/>.</param>
public sealed record AnalyticsBucketDto(string Start, long Views, long Emails, long Sales, long RevenueCents, long Downloads);

public sealed record AnalyticsBestMonthDto(
    AnalyticsMonthValueDto? Views,
    AnalyticsMonthValueDto? Emails,
    AnalyticsMonthValueDto? Sales,
    AnalyticsMonthValueDto? Revenue,
    AnalyticsMonthValueDto? Downloads);

/// <param name="Month">"yyyy-MM" (local).</param>
public sealed record AnalyticsMonthValueDto(string Month, long Value);

/// <param name="RefundCount">The product's orders refunded in the period (by refund time).</param>
/// <param name="RefundedCents">Their AmountCents.</param>
/// <param name="CreatorRevenueCents">All the creator's paid revenue in the same period — for "share of your revenue".</param>
public sealed record AnalyticsProductExtrasDto(int RefundCount, long RefundedCents, long CreatorRevenueCents);

/// <summary>The period's refunds, customers and sign-ups — of the workspace, or of one product (R6.3). No fee.</summary>
/// <param name="RefundCount">Orders refunded in the period (by refund time).</param>
/// <param name="RefundedCents">Their AmountCents.</param>
/// <param name="Customers">Distinct buyers (lower-cased email) with a Paid order counted in the period (PaidAt, else CreatedAt).</param>
/// <param name="PreviousCustomers">The same for the previous window; null for "all".</param>
/// <param name="ReturningCustomers">Of <paramref name="Customers"/>, those with a Paid order counted before the period start
/// (of this product for a product, of any product for the workspace); 0 for "all", which starts at the beginning.</param>
/// <param name="Signups">Distinct emails that signed up in the period: on the product's pages for a product, new contacts
/// for the workspace.</param>
/// <param name="SignupsStillSubscribed">Of <paramref name="Signups"/>, those not unsubscribed from the creator now.</param>
public sealed record AnalyticsExtrasDto(
    int RefundCount,
    long RefundedCents,
    int Customers,
    int? PreviousCustomers,
    int ReturningCustomers,
    int Signups,
    int SignupsStillSubscribed);

/// <summary>Revenue and sales per product for one period (paid orders by PaidAt, else CreatedAt), best first.</summary>
/// <param name="Products">At most 20 products with revenue, sorted by revenue desc (ties by name).</param>
/// <param name="TotalRevenueCents">Every product's revenue in the period, the listed ones and the rest.</param>
/// <param name="OtherRevenueCents">Revenue of the products beyond <paramref name="Products"/> (0 when all are listed).</param>
/// <param name="ProductsWithSales">Products with revenue in the period.</param>
/// <param name="Selected">The product asked for with <c>productId</c>, wherever it ranks; null without one.</param>
public sealed record AnalyticsProductBreakdownDto(
    string Period,
    AnalyticsRangeDto Range,
    string Currency,
    long TotalRevenueCents,
    List<AnalyticsProductRevenueDto> Products,
    long OtherRevenueCents,
    int ProductsWithSales,
    AnalyticsSelectedProductDto? Selected);

public sealed record AnalyticsProductRevenueDto(Guid PublicId, string Name, long RevenueCents, long Sales);
