namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>What the buyer's purchase success page shows. Contains no internal ids; the email is masked.</summary>
/// <param name="OrderNumber">Display reference derived from the order's public id (see <c>OrderNumbers</c>).</param>
/// <param name="Status">"Pending" until the payment webhook has arrived, then "Paid" (or "Refunded" / "Failed").</param>
/// <param name="BuyerEmail">Masked: first character and domain, e.g. "a•••@gmail.com". Null while Pending when the
/// buyer typed it into Stripe Checkout (it arrives with the payment webhook).</param>
/// <param name="FileName">The product's document, for the download button; null when it has none.</param>
/// <param name="AccessToken">The buyer's personal download link token (<c>/d/{token}</c>) once paid; null while Pending
/// and after a refund. The success page starts the download with it.</param>
public sealed record OrderReceiptDto(
    string OrderNumber,
    string Status,
    string? BuyerFirstName,
    string? BuyerEmail,
    string ProductName,
    int AmountCents,
    string Currency,
    DateTimeOffset? PaidAt,
    OrderReceiptCreatorDto Creator,
    string? AccessToken,
    string? FileName,
    long? FileSizeBytes);

/// <param name="Name">The creator's brand name (falls back to the workspace name).</param>
public sealed record OrderReceiptCreatorDto(
    string Name,
    string Slug,
    string? BrandColor,
    string? LogoUrl,
    string? SupportEmail);
