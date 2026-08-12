namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>
/// Per-landing-page purchase count + revenue, for a whole creator in one round trip — powers the
/// Purchases/Revenue columns on the landing pages list (same "merge a summary into the list" pattern
/// as <c>IPageViewService.GetViewsSummaryByCreatorAsync</c> for view counts).
/// </summary>
public sealed record LandingPageOrdersSummaryDto(
    Guid LandingPagePublicId,
    int PurchaseCount,
    int TotalRevenueCents,
    string? Currency);
