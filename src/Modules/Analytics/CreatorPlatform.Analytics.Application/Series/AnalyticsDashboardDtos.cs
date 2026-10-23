namespace CreatorPlatform.Analytics.Application.Series;

/// <summary>The dashboard's top pages for one period (R6.4): non-archived pages with activity in the period, best first.</summary>
/// <param name="PageCount">Non-archived pages in scope, with or without activity.</param>
/// <param name="Pages">Ranked by revenue, then emails, then views (desc), then title; pages without views, emails or
/// sales in the period are left out.</param>
public sealed record AnalyticsPagesDto(
    string Period,
    AnalyticsRangeDto Range,
    string Currency,
    int PageCount,
    List<AnalyticsPageRowDto> Pages);

/// <param name="Type">"Sales" | "LeadGen".</param>
/// <param name="Status">"Published" | "Draft".</param>
/// <param name="Emails">Distinct emails captured on the page in the period.</param>
/// <param name="Sales">Paid orders placed through the page, counted in the period (PaidAt, else CreatedAt).</param>
public sealed record AnalyticsPageRowDto(
    Guid PublicId,
    string Title,
    string Slug,
    string Type,
    string Status,
    Guid? ProductPublicId,
    string? ProductName,
    string? ProductThumbnailUrl,
    long Views,
    long Emails,
    long Sales,
    long RevenueCents);

/// <summary>One product within the period's revenue ranking (R6.4).</summary>
/// <param name="Rank">1-based by revenue desc (ties by name) among the products with revenue; null when it has none.</param>
public sealed record AnalyticsSelectedProductDto(Guid PublicId, string Name, long RevenueCents, long Sales, int? Rank);

/// <summary>A product's newest sign-ups across the lead pages that give it away (R6.4): one row per email, its latest capture.</summary>
/// <param name="Total">Distinct emails that ever signed up on those pages.</param>
public sealed record ProductSignupsDto(int Total, List<ProductSignupDto> Signups);

/// <param name="Unsubscribed">The email has unsubscribed from the creator since.</param>
public sealed record ProductSignupDto(
    string Email,
    DateTimeOffset CapturedAt,
    bool Unsubscribed,
    Guid LandingPagePublicId,
    string LandingPageTitle);
