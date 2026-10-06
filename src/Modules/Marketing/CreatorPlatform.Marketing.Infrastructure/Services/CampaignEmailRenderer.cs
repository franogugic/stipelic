using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Marketing.Application.Interfaces;
using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>
/// <c>designer-prototype/emails/campaign.html</c>: the creator's text in Luma's email-safe layout, with the creator's
/// header (logo or monogram in their colour), a brand-coloured button and the unsubscribe footer. The
/// <c>{{UNSUBSCRIBE_URL}}</c> placeholder is left in both bodies and <c>{{OPEN_PIXEL_URL}}</c> in the HTML body only (the
/// plain-text version carries no tracking pixel), for the send pipeline to substitute per recipient. Every value is
/// HTML-encoded.
/// </summary>
public sealed class CampaignEmailRenderer : ICampaignEmailRenderer
{
    public const string UnsubscribePlaceholder = "{{UNSUBSCRIBE_URL}}";
    public const string OpenPixelPlaceholder = "{{OPEN_PIXEL_URL}}";

    public CampaignEmailContent Render(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string? supportEmail = null)
    {
        var brand = BrandPalette.From(primaryColor);
        var hasCta = !string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl);
        var lines = bodyText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var card = string.Concat(lines.Select(line => Paragraph(E(line))))
            + (hasCta ? Button(ctaLabel!, ctaUrl!, brand) : string.Empty);

        var replyTo = string.IsNullOrWhiteSpace(supportEmail) ? string.Empty : $" · Reply to {E(supportEmail)}";
        var footer =
            $"You’re receiving this because you signed up at {E(brandName)}.<br>" +
            $"""<a class="muted" href="{UnsubscribePlaceholder}" style="color:#6B695F; text-decoration:underline;">Unsubscribe</a>{replyTo}<br><br>Sent with Luma""";

        var html = Document(subject, FirstLine(lines), CreatorHeader(brandName, logoUrl, brand), card, footer, brand.TextDark)
            .Replace("</body>", $"""  <img src="{OpenPixelPlaceholder}" width="1" height="1" alt="" style="display:block;border:0;width:1px;height:1px">\n</body>""");

        var plainText = string.Join("\n\n", lines)
            + (hasCta ? $"\n\n{ctaLabel}: {ctaUrl}" : string.Empty)
            + $"\n\n---\nYou’re receiving this because you signed up at {brandName}.\nUnsubscribe: {UnsubscribePlaceholder}"
            + (string.IsNullOrWhiteSpace(supportEmail) ? string.Empty : $"\nReply to {supportEmail}")
            + "\nSent with Luma\n";

        return new CampaignEmailContent(subject, html, plainText);
    }

    /// <summary>The inbox preview line: the first line of the message.</summary>
    private static string FirstLine(string[] lines) => lines.Length == 0 ? string.Empty : lines[0].Trim();
}
