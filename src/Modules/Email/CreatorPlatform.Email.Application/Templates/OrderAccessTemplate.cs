using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <param name="FileLabel">"ZIP · 17.5 MB" (<see cref="EmailFileLabel"/>); null when the product has no file.</param>
/// <param name="DownloadUrl">The buyer's personal access link (<c>{FrontendBaseUrl}/d/{token}</c>).</param>
/// <summary>Everything the buyer's order email shows. The creator's brand fields may be missing (no settings saved).</summary>
public sealed record OrderAccessEmail(
    string OrderNumber,
    string? BuyerName,
    string BuyerEmail,
    string ProductName,
    string? FileLabel,
    string? ProductThumbnailUrl,
    int AmountCents,
    string Currency,
    DateTimeOffset PaidAt,
    string DownloadUrl,
    string CreatorName,
    string? BrandColor,
    string? LogoUrl,
    string? SupportEmail);

/// <summary><c>designer-prototype/emails/order-access.html</c> — creator-branded: their colour (contrast-adjusted for
/// text), logo or monogram, name and support address; Luma's accent when they have no colour.</summary>
public static class OrderAccessTemplate
{
    public static string SubjectFor(string productName) => $"Your {productName}";

    public static RenderedEmail Render(OrderAccessEmail order)
    {
        var subject = SubjectFor(order.ProductName);
        var brand = BrandPalette.From(order.BrandColor);
        var firstName = FirstName(order.BuyerName);
        var price = EmailMoney.Format(order.AmountCents, order.Currency);

        var thumbnail = string.IsNullOrWhiteSpace(order.ProductThumbnailUrl)
            ? $"""<td width="80" height="80" bgcolor="{brand.Fill}" style="width:80px; height:80px; border-radius:10px; background-color:{brand.Fill};">&nbsp;</td>"""
            : $"""<td width="80" height="80" style="width:80px; height:80px;"><img src="{E(order.ProductThumbnailUrl)}" width="80" height="80" alt="" style="display:block; width:80px; height:80px; border:0; border-radius:10px; object-fit:cover;"></td>""";

        var fileLabel = order.FileLabel is null
            ? string.Empty
            : $"""<p class="muted" style="margin:4px 0 0; font-size:13px; line-height:20px; color:#6B695F;">{E(order.FileLabel)}</p>""";

        var product = $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="bg-soft" style="margin:24px 0 0; background-color:#F4F3EE; border-radius:14px;"><tr>
                              <td class="stack" width="96" style="padding:16px; width:96px;">
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>{thumbnail}</tr></table>
                              </td>
                              <td class="stack" style="padding:16px 18px 16px 0; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;">
                                <p class="text" style="margin:0; font-size:16px; line-height:22px; font-weight:600; color:#17160F;">{E(order.ProductName)}</p>{fileLabel}
                                <p class="text" style="margin:8px 0 0; font-family:'Instrument Serif', Georgia, 'Times New Roman', serif; font-size:26px; line-height:28px; color:#17160F;">{E(price)}</p>
                              </td>
                            </tr></table>
            """;

        var questions = string.IsNullOrWhiteSpace(order.SupportEmail)
            ? string.Empty
            : $"""<p class="text" style="margin:24px 0 0; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:14px; line-height:22px; color:#17160F;">Questions about your purchase? Just reply to this email or write to <a class="brand-text" href="mailto:{E(order.SupportEmail)}" style="color:{brand.TextLight}; font-weight:600;">{E(order.SupportEmail)}</a>.</p>""";

        var heading = firstName is null
            ? Heading("Thank you", "!", "brand-text", brand.TextLight)
            : Heading("Thank you, ", $"{firstName}!", "brand-text", brand.TextLight);

        var card =
            heading +
            Paragraph($"Your order is confirmed and your download started in your browser. Here’s your personal link to download {Strong(order.ProductName)} again — any time, on any device.") +
            product +
            Button("Download your file", order.DownloadUrl, brand) +
            FallbackLink(order.DownloadUrl) +
            KeyValues(
                Row("Order", $"#{order.OrderNumber}"),
                Row("Paid", $"{price} · {Date(order.PaidAt)}"),
                Row("Sent to", order.BuyerEmail)) +
            questions;

        var html = Document(
            subject,
            "Thank you for your order — here’s your download link.",
            CreatorHeader(order.CreatorName, order.LogoUrl, brand),
            card,
            $"Sold by {E(order.CreatorName)} through Luma · Card payment processed by Stripe<br>{KeepThisEmail}",
            brand.TextDark);

        var support = string.IsNullOrWhiteSpace(order.SupportEmail)
            ? string.Empty
            : $"\nQuestions about your purchase? Just reply to this email or write to {order.SupportEmail}.\n";
        var text = $"""
            {(firstName is null ? "Thank you!" : $"Thank you, {firstName}!")}

            Your order is confirmed and your download started in your browser. Here’s your personal link to download {order.ProductName} again — any time, on any device.

            Download your file:
            {order.DownloadUrl}

            Order: #{order.OrderNumber}
            Paid: {price} · {Date(order.PaidAt)}
            Sent to: {order.BuyerEmail}
            {support}
            Sold by {order.CreatorName} through Luma · Card payment processed by Stripe
            {KeepThisEmail}
            """;

        return new RenderedEmail(subject, html, text);
    }

    /// <summary>Shared with the lead-magnet email: both carry a personal download link.</summary>
    public const string KeepThisEmail = "Keep this email — your download link is in it. It’s personal, so please don’t share it.";

    private static string? FirstName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }
}
