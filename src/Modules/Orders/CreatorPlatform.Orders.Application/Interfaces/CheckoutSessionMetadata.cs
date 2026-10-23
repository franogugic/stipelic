namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Metadata keys a product sale's Checkout session carries (on the session and its PaymentIntent).</summary>
public static class CheckoutSessionMetadata
{
    /// <summary>The order's PublicId — the order is saved before the session, so the webhook can always find it.</summary>
    public const string OrderId = "orderId";

    /// <summary>Present on every sale session: tells it apart from a plan subscription's session.</summary>
    public const string ProductId = "productId";
}
