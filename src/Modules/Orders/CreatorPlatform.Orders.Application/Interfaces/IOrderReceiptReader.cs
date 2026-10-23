using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Everything the receipt shows, read in one query by the Stripe Checkout session id.</summary>
/// <param name="AccessToken">The buyer's personal link token while it is on (null before payment and after a refund).</param>
public sealed record OrderReceiptRow(
    Guid OrderPublicId,
    string Status,
    string? BuyerName,
    string? BuyerEmail,
    int AmountCents,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string ProductName,
    string? FileName,
    long? FileSizeBytes,
    string CreatorName,
    string CreatorSlug,
    string? BrandName,
    string? BrandColor,
    string? LogoUrl,
    string? SupportEmail,
    string? AccessToken);

public interface IOrderReceiptReader
{
    Task<OrderReceiptRow?> GetBySessionIdAsync(string checkoutSessionId, CancellationToken ct);
}
