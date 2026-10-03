namespace CreatorPlatform.Products.Application.Interfaces;

/// <summary>Products' local read-through to landing pages, so a product can't be pulled from sale while a live page sells it.</summary>
public interface ILandingPageContextProvider
{
    Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct);
}
