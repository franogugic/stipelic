namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>Paid orders of one landing page per analytics period (see <c>StatsPeriods</c>). A period counts orders
/// by <c>PaidAt</c>; all time counts every order currently Paid, so refunded orders drop out of every period.</summary>
/// <param name="Currency">The creator's currency; null when the page has no orders at all.</param>
public sealed record LandingPageSalesByPeriodDto(
    PeriodSalesDto Today,
    PeriodSalesDto Last7Days,
    PeriodSalesDto Last30Days,
    PeriodSalesDto AllTime,
    string? Currency);

public sealed record PeriodSalesDto(int PurchaseCount, long RevenueCents);
