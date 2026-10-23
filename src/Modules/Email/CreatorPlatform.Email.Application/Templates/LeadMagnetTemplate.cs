using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>What the "your free download" email shows to someone who signed up on a lead page.</summary>
/// <param name="FileLabel">"PDF · 8.2 MB" (<see cref="EmailFileLabel"/>).</param>
/// <param name="DownloadUrl">The sign-up's personal access link (<c>{FrontendBaseUrl}/d/{token}</c>).</param>
/// <param name="UnsubscribeUrl">The creator's list unsubscribe link; null for an email that already unsubscribed —
/// they still get the document they asked for, but no "you'll also get emails" line.</param>
public sealed record LeadMagnetEmail(
    string SignupEmail,
    string ProductName,
    string? FileLabel,
    string? ProductThumbnailUrl,
    DateTimeOffset SignedUpAt,
    string DownloadUrl,
    string CreatorName,
    string? BrandColor,
    string? LogoUrl,
    string? SupportEmail,
    string? UnsubscribeUrl);

/// <summary><c>designer-prototype/emails/lead-download.html</c> — creator-branded like the order email. Transactional:
/// the visitor asked for this document, so it is sent even to an unsubscribed email.</summary>
public static class LeadMagnetTemplate
{
    public static string SubjectFor(string productName) => $"Your free download: {productName}";

    public static RenderedEmail Render(LeadMagnetEmail lead)
    {
        var subject = SubjectFor(lead.ProductName);
        var brand = BrandPalette.From(lead.BrandColor);

        var thumbnail = string.IsNullOrWhiteSpace(lead.ProductThumbnailUrl)
            ? $"""<td width="80" height="80" bgcolor="{brand.Fill}" style="width:80px; height:80px; border-radius:10px; background-color:{brand.Fill};">&nbsp;</td>"""
            : $"""<td width="80" height="80" style="width:80px; height:80px;"><img src="{E(lead.ProductThumbnailUrl)}" width="80" height="80" alt="" style="display:block; width:80px; height:80px; border:0; border-radius:10px; object-fit:cover;"></td>""";

        var fileLabel = lead.FileLabel is null
            ? string.Empty
            : $"""<p class="muted" style="margin:4px 0 0; font-size:13px; line-height:20px; color:#6B695F;">{E(lead.FileLabel)}</p>""";

        var product = $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="bg-soft" style="margin:24px 0 0; background-color:#F4F3EE; border-radius:14px;"><tr>
                              <td class="stack" width="96" style="padding:16px; width:96px;">
                                <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>{thumbnail}</tr></table>
                              </td>
                              <td class="stack" style="padding:16px 18px 16px 0; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;">
                                <p class="text" style="margin:0; font-size:16px; line-height:22px; font-weight:600; color:#17160F;">{E(lead.ProductName)}</p>{fileLabel}
                                <p class="text" style="margin:8px 0 0; font-family:'Instrument Serif', Georgia, 'Times New Roman', serif; font-size:26px; line-height:28px; color:#17160F;">Free</p>
                              </td>
                            </tr></table>
            """;

        var questions = string.IsNullOrWhiteSpace(lead.SupportEmail)
            ? string.Empty
            : $"""<p class="text" style="margin:24px 0 0; font-family:Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:14px; line-height:22px; color:#17160F;">Questions about the guide? Just reply to this email or write to <a class="brand-text" href="mailto:{E(lead.SupportEmail)}" style="color:{brand.TextLight}; font-weight:600;">{E(lead.SupportEmail)}</a>.</p>""";

        var card =
            Heading("Your free ", "download", "brand-text", brand.TextLight) +
            Paragraph($"Thanks for signing up on {E(lead.CreatorName)}’s page. Here’s your personal link to download {Strong(lead.ProductName)} — now, and any time later, on any device.") +
            product +
            Button("Download the guide", lead.DownloadUrl, brand) +
            FallbackLink(lead.DownloadUrl) +
            KeyValues(
                Row("Signed up", Date(lead.SignedUpAt)),
                Row("Sent to", lead.SignupEmail)) +
            questions;

        var footer = $"Sent by {E(lead.CreatorName)} through Luma because you asked for this guide.<br>{E(OrderAccessTemplate.KeepThisEmail)}";
        if (lead.UnsubscribeUrl is not null)
            footer += $"<br>You’ll also get {E(lead.CreatorName)}’s emails · {FooterLink("Unsubscribe", lead.UnsubscribeUrl)}";

        var html = Document(
            subject,
            $"Here’s your personal link to download {lead.ProductName}.",
            CreatorHeader(lead.CreatorName, lead.LogoUrl, brand),
            card,
            footer,
            brand.TextDark);

        var support = string.IsNullOrWhiteSpace(lead.SupportEmail)
            ? string.Empty
            : $"\nQuestions about the guide? Just reply to this email or write to {lead.SupportEmail}.\n";
        var unsubscribe = lead.UnsubscribeUrl is null
            ? string.Empty
            : $"\nYou’ll also get {lead.CreatorName}’s emails. Unsubscribe: {lead.UnsubscribeUrl}";
        var text = $"""
            Your free download

            Thanks for signing up on {lead.CreatorName}’s page. Here’s your personal link to download {lead.ProductName} — now, and any time later, on any device.

            Download the guide:
            {lead.DownloadUrl}

            Signed up: {Date(lead.SignedUpAt)}
            Sent to: {lead.SignupEmail}
            {support}
            Sent by {lead.CreatorName} through Luma because you asked for this guide.
            {OrderAccessTemplate.KeepThisEmail}{unsubscribe}
            """;

        return new RenderedEmail(subject, html, text);
    }
}
