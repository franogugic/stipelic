using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Receipts;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderReceiptService : IOrderReceiptService
{
    /// <summary>The success page is opened right after checkout; an old session id is never a valid way in.</summary>
    private static readonly TimeSpan ReceiptWindow = TimeSpan.FromHours(24);
    private const string NotFoundMessage = "Order not found.";

    private readonly IOrderReceiptReader _reader;

    public OrderReceiptService(IOrderReceiptReader reader)
    {
        _reader = reader;
    }

    public async Task<OrderReceiptDto> GetBySessionIdAsync(string? checkoutSessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(checkoutSessionId))
            throw new NotFoundException(NotFoundMessage);

        var row = await _reader.GetBySessionIdAsync(checkoutSessionId.Trim(), ct);

        // Unknown and expired answer the same, so the endpoint can't be used to probe for sessions.
        if (row is null || row.CreatedAt < DateTimeOffset.UtcNow - ReceiptWindow)
            throw new NotFoundException(NotFoundMessage);

        return new OrderReceiptDto(
            OrderNumbers.From(row.OrderPublicId),
            row.Status,
            FirstName(row.BuyerName),
            row.BuyerEmail is null ? null : EmailMask.Mask(row.BuyerEmail),
            row.ProductName,
            row.AmountCents,
            row.Currency,
            row.PaidAt,
            new OrderReceiptCreatorDto(
                string.IsNullOrWhiteSpace(row.BrandName) ? row.CreatorName : row.BrandName,
                row.CreatorSlug,
                row.BrandColor,
                row.LogoUrl,
                row.SupportEmail));
    }

    private static string? FirstName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }
}
