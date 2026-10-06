using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Shared.Application.Csv;

namespace CreatorPlatform.Orders.Application.Services;

/// <summary>The orders export format:
/// <c>order_id,created_at,customer_name,customer_email,product,landing_page,status,amount,platform_fee,net,currency</c>.</summary>
public static class OrdersCsv
{
    public static readonly string Header = CsvFormatter.Row(
        "order_id", "created_at", "customer_name", "customer_email", "product", "landing_page",
        "status", "amount", "platform_fee", "net", "currency");

    /// <summary>Amounts are decimal strings with two decimals (29.00); created_at is ISO 8601 UTC; currency is
    /// the ISO 4217 code (EUR); status is the same value the orders API returns (Paid, Pending, …).</summary>
    public static string Row(OrderDto order) => CsvFormatter.Row(
        order.PublicId.ToString(),
        CsvFormatter.Timestamp(order.CreatedAt),
        order.Name,
        order.Email,
        order.ProductName,
        order.LandingPageTitle,
        order.Status,
        CsvFormatter.Money(order.AmountCents),
        CsvFormatter.Money(order.PlatformFeeCents),
        CsvFormatter.Money(order.NetAmountCents),
        order.Currency.ToUpperInvariant());

    public static string FileName(string slug, DateTimeOffset now) =>
        $"orders-{slug}-{now.UtcDateTime:yyyy-MM-dd}.csv";
}
