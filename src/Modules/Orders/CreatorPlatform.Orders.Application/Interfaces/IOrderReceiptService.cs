using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IOrderReceiptService
{
    /// <summary>The receipt for a Checkout session, for the purchase success page. 404 for a blank or unknown session
    /// id and for a session older than 24 hours. "Pending" while the payment webhook hasn't arrived yet.</summary>
    Task<OrderReceiptDto> GetBySessionIdAsync(string? checkoutSessionId, CancellationToken ct);
}
