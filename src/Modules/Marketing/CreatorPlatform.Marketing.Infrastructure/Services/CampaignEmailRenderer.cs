using System.Net;
using CreatorPlatform.Marketing.Application.Interfaces;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>Brand-neutral-except-for-creator-colors HTML template: inline styles only (no external CSS —
/// most email clients strip &lt;style&gt; blocks or class-based CSS), max-width 600px, plain-text version
/// always produced alongside. The <c>{{UNSUBSCRIBE_URL}}</c> placeholder is left in both bodies for the
/// send pipeline to substitute per recipient.</summary>
public sealed class CampaignEmailRenderer : ICampaignEmailRenderer
{
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:0;background-color:#f5f5f4;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f5f5f4;padding:32px 0;">
            <tr>
              <td align="center">
                <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background-color:#ffffff;border-radius:12px;padding:32px;">
                  <tr><td>
                    __LOGO__
                    __BODY__
                    __CTA__
                    <hr style="border:none;border-top:1px solid #eeeeee;margin:32px 0 16px;" />
                    <p style="font-size:12px;color:#999999;margin:0 0 8px;">Sent via Creator Platform</p>
                    <p style="font-size:12px;color:#999999;margin:0;"><a href="{{UNSUBSCRIBE_URL}}" style="color:#999999;">Unsubscribe</a> from these emails.</p>
                  </td></tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private const string PlainTextTemplate = """
        __BODY__
        __CTA__
        ---
        Sent via Creator Platform
        Unsubscribe: {{UNSUBSCRIBE_URL}}
        """;

    public CampaignEmailContent Render(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        string brandName,
        string? logoUrl,
        string primaryColor)
    {
        var encodedBrandName = WebUtility.HtmlEncode(brandName);
        var hasCta = !string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl);

        var logoHtml = !string.IsNullOrWhiteSpace(logoUrl)
            ? $"""<img src="{WebUtility.HtmlEncode(logoUrl)}" alt="{encodedBrandName}" style="max-height:40px;margin-bottom:16px;" />"""
            : $"""<p style="font-weight:700;font-size:18px;margin:0 0 16px;color:#111111;">{encodedBrandName}</p>""";

        var ctaHtml = hasCta
            ? $"""
              <table role="presentation" cellpadding="0" cellspacing="0" style="margin:24px 0;">
                <tr>
                  <td style="border-radius:8px;background-color:{primaryColor};">
                    <a href="{WebUtility.HtmlEncode(ctaUrl)}" style="display:inline-block;padding:12px 24px;color:#ffffff;text-decoration:none;font-weight:600;">{WebUtility.HtmlEncode(ctaLabel)}</a>
                  </td>
                </tr>
              </table>
              """
            : string.Empty;

        var html = HtmlTemplate
            .Replace("__LOGO__", logoHtml)
            .Replace("__BODY__", BuildBodyHtml(bodyText))
            .Replace("__CTA__", ctaHtml);

        var ctaPlainText = hasCta ? $"\n{ctaLabel}: {ctaUrl}\n" : string.Empty;
        var plainText = PlainTextTemplate
            .Replace("__BODY__", bodyText)
            .Replace("__CTA__", ctaPlainText);

        return new CampaignEmailContent(subject, html, plainText);
    }

    private static string BuildBodyHtml(string bodyText)
    {
        var paragraphs = bodyText
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => $"""<p style="margin:0 0 16px;line-height:1.5;color:#333333;">{WebUtility.HtmlEncode(line)}</p>""");

        return string.Join("\n", paragraphs);
    }
}
