namespace CreatorPlatform.Orders.Application.Interfaces;

/// <param name="CustomerEmail">Stripe's <c>customer_details.email</c>: what the buyer typed (or the prefill).</param>
/// <param name="CustomerName">Stripe's <c>customer_details.name</c>.</param>
/// <param name="CustomerCountry">Stripe's <c>customer_details.address.country</c> (Checkout collects it for card payments).</param>
/// <param name="Metadata">The session's metadata; a sale session carries <c>orderId</c> (the order's PublicId) and <c>productId</c>.</param>
public sealed record OrderCheckoutCompletedDto(
    string SessionId,
    string? PaymentIntentId,
    string? CustomerEmail = null,
    string? CustomerName = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? CustomerCountry = null);

/// <param name="ChargeAmountCents">The charge's amount in its own currency (Adaptive Pricing may make it the buyer's).</param>
/// <param name="ChargeAmountRefundedCents">Cumulative refunded amount of the charge, same currency.</param>
/// <param name="FullyRefunded">Stripe's <c>charge.refunded</c> flag.</param>
/// <param name="Metadata">The session's metadata, as on <see cref="OrderCheckoutCompletedDto"/>.</param>
public sealed record OrderCheckoutExpiredDto(string SessionId, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OrderChargeRefundedDto(
    string PaymentIntentId, string ChargeId, long ChargeAmountCents, long ChargeAmountRefundedCents, bool FullyRefunded);

public interface IOrderWebhookService
{
    Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct);

    /// <summary>checkout.session.expired: the session can no longer be paid, so its Pending order becomes Failed.</summary>
    Task HandleCheckoutSessionExpiredAsync(OrderCheckoutExpiredDto data, CancellationToken ct);

    Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct);
}
