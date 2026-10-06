namespace CreatorPlatform.Products.Application.Interfaces;

/// <summary>Products' local read-through to captured emails.</summary>
public interface IAnalyticsContextProvider
{
    /// <summary>Distinct emails captured with the product or on a landing page that sells it — the same union as the
    /// Product campaign audience, before unsubscribes are taken out (an unsubscribed person is still a contact).</summary>
    Task<int> CountContactsAsync(int productId, CancellationToken ct);
}
