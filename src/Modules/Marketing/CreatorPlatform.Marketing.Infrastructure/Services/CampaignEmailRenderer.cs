using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Mail;
using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>
/// <c>designer-prototype/emails/campaign.html</c>: the creator's text in Luma's email-safe layout, with the creator's
/// header (logo or monogram in their colour), a brand-coloured button and the unsubscribe footer. The
/// <c>{{UNSUBSCRIBE_URL}}</c> placeholder is left in both bodies and <c>{{OPEN_PIXEL_URL}}</c> in the HTML body only (the
/// plain-text version carries no tracking pixel), for the send pipeline to substitute per recipient. Every http(s) URL
/// in the body becomes a link (its visible text stays the URL), and every link — those and the button — points at a
/// per-render placeholder (<see cref="RenderedLink.Placeholder"/>) in both bodies, replaced per recipient by its
/// tracked URL. The unsubscribe link and the pixel are not links of the campaign. Every value is HTML-encoded.
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
        string? supportEmail = null,
        CampaignAudienceReason reason = CampaignAudienceReason.SignedUp)
    {
        var brand = BrandPalette.From(primaryColor);
        var hasCta = !string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl);
        var lines = bodyText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // A fresh random part per render: text a creator types can never be mistaken for a link placeholder.
        var nonce = Guid.NewGuid().ToString("N");
        var links = CampaignLinkExtractor.Extract(lines, ctaLabel, ctaUrl)
            .Select(link => new RenderedLink(link.Position, link.Url, link.Kind, link.Label, $"{{{{LINK_{nonce}_{link.Position}}}}}"))
            .ToList();
        var placeholders = links.ToDictionary(link => link.Url, link => link.Placeholder, StringComparer.Ordinal);

        var card = string.Concat(lines.Select(line => Paragraph(LinkedHtml(line, placeholders, brand))))
            + (hasCta ? Button(ctaLabel!, placeholders[ctaUrl!.Trim()], brand) : string.Empty);

        var replyTo = string.IsNullOrWhiteSpace(supportEmail) ? string.Empty : $" · Reply to {E(supportEmail)}";
        // Why they get it: they signed up at the brand, or (buyers audience) they bought from it.
        var why = reason == CampaignAudienceReason.Bought ? "you bought from" : "you signed up at";
        var footer =
            $"You’re receiving this because {why} {E(brandName)}.<br>" +
            $"""<a class="muted" href="{UnsubscribePlaceholder}" style="color:#6B695F; text-decoration:underline;">Unsubscribe</a>{replyTo}<br><br>Sent with Luma""";

        var html = Document(subject, FirstLine(lines), CreatorHeader(brandName, logoUrl, brand), card, footer, brand.TextDark)
            .Replace("</body>", $"""  <img src="{OpenPixelPlaceholder}" width="1" height="1" alt="" style="display:block;border:0;width:1px;height:1px">\n</body>""");

        var plainText = string.Join("\n\n", lines.Select(line => LinkedText(line, placeholders)))
            + (hasCta ? $"\n\n{ctaLabel}: {placeholders[ctaUrl!.Trim()]}" : string.Empty)
            + $"\n\n---\nYou’re receiving this because {why} {brandName}.\nUnsubscribe: {UnsubscribePlaceholder}"
            + (string.IsNullOrWhiteSpace(supportEmail) ? string.Empty : $"\nReply to {supportEmail}")
            + "\nSent with Luma\n";

        return new CampaignEmailContent(subject, html, plainText, links);
    }

    /// <summary>The line HTML-encoded, its URLs as links to their placeholders, in the brand's text colour.</summary>
    private static string LinkedHtml(string line, Dictionary<string, string> placeholders, BrandPalette brand) =>
        string.Concat(CampaignLinkExtractor.Segment(line).Select(segment => segment.IsUrl
            ? $"""<a href="{placeholders[segment.Text]}" target="_blank" style="color:{brand.TextLight}; text-decoration:underline; word-break:break-all;">{E(segment.Text)}</a>"""
            : E(segment.Text)));

    /// <summary>The plain-text line with each URL replaced by its placeholder.</summary>
    private static string LinkedText(string line, Dictionary<string, string> placeholders) =>
        string.Concat(CampaignLinkExtractor.Segment(line).Select(segment => segment.IsUrl ? placeholders[segment.Text] : segment.Text));

    /// <summary>The inbox preview line: the first line of the message.</summary>
    private static string FirstLine(string[] lines) => lines.Length == 0 ? string.Empty : lines[0].Trim();
}
