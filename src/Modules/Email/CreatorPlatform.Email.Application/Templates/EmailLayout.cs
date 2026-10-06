using System.Globalization;
using System.Net;
using System.Text;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>
/// The email-safe Luma layout from <c>designer-prototype/emails/</c>: tables, inline styles, 600 px, a bulletproof
/// button, a hidden preheader, dark mode through <c>prefers-color-scheme</c> and <c>[data-ogsc]</c>. The markup is the
/// prototype's; only the values change. Every helper HTML-encodes its text arguments; parameters named
/// <c>html</c> take markup the caller already built from encoded parts.
/// </summary>
public static class EmailLayout
{
    private const string BodyFont = "Geist, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif";
    private const string SerifFont = "'Instrument Serif', Georgia, 'Times New Roman', serif";
    private const string MonoFont = "'Geist Mono', 'SF Mono', Menlo, Consolas, monospace";

    public static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>The full document. <paramref name="brandTextDark"/> is the dark-mode colour of <c>.brand-text</c>
    /// (creator-branded emails); Luma's own emails pass the default.</summary>
    public static string Document(
        string title, string preheader, string headerHtml, string cardHtml, string footerHtml,
        string brandTextDark = BrandPalette.DefaultBrandTextDark)
    {
        var style = Style.Replace("__BRAND_TEXT_DARK__", brandTextDark);
        return $$"""
            <!doctype html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="x-apple-disable-message-reformatting">
              <meta name="format-detection" content="telephone=no, date=no, address=no, email=no">
              <meta name="color-scheme" content="light dark">
              <meta name="supported-color-schemes" content="light dark">
              <title>{{E(title)}}</title>
              <link href="https://fonts.googleapis.com/css2?family=Geist:wght@400;600&amp;family=Instrument+Serif:ital@0;1&amp;display=swap" rel="stylesheet">
              <style>
            {{style}}  </style>
            </head>
            <body class="bg-page" style="margin:0; padding:0; background-color:#F2F1EC;">
              <div style="display:none; max-height:0; overflow:hidden; opacity:0; mso-hide:all;">{{E(preheader)}}&#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; &#847; &zwnj; &nbsp; </div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="bg-page" style="background-color:#F2F1EC;">
                <tr>
                  <td align="center" style="padding:32px 12px 40px;">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" class="container" style="width:600px; max-width:600px;">
                      {{headerHtml}}
                      <tr>
                        <td class="bg-card" style="background-color:#FFFFFF; border:1px solid #E4E2D9; border-radius:16px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr><td class="px" style="padding:44px 48px 40px; font-family:{{BodyFont}}; font-size:16px; line-height:26px; color:#17160F;">{{cardHtml}}</td></tr>
                          </table>
                        </td>
                      </tr>
                      <tr><td class="px" style="padding:24px 48px 0; font-family:{{BodyFont}}; font-size:12px; line-height:19px; color:#6B695F; text-align:center;">{{footerHtml}}</td></tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private const string Style = """
    :root { color-scheme: light dark; supported-color-schemes: light dark; }
    body { margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
    table { border-collapse: collapse; }
    a { text-decoration: none; }
    @media (max-width: 620px) {
      .container { width: 100% !important; }
      .px { padding-left: 24px !important; padding-right: 24px !important; }
      .h1 { font-size: 32px !important; line-height: 36px !important; }
      .stack { display: block !important; width: 100% !important; }
    }
    @media (prefers-color-scheme: dark) {
      .bg-page { background-color: #0A0A09 !important; }
      .bg-card { background-color: #181816 !important; border-color: #2A2925 !important; }
      .bg-soft { background-color: #201F1C !important; }
      .text { color: #F2F1EB !important; }
      .muted { color: #A9A79D !important; }
      .line { border-color: #2A2925 !important; }
      .accent-text { color: #D4FA5A !important; }
      .btn-ink { background-color: #D4FA5A !important; }
      .btn-ink a { color: #0A0A09 !important; }
      .brand-text { color: __BRAND_TEXT_DARK__ !important; }
      .warn { background-color: #2A2112 !important; }
      .warn-text { color: #F2B544 !important; }
    }
    [data-ogsc] .bg-page { background-color: #0A0A09 !important; }
    [data-ogsc] .bg-card { background-color: #181816 !important; }
    [data-ogsc] .bg-soft { background-color: #201F1C !important; }
    [data-ogsc] .text { color: #F2F1EB !important; }
    [data-ogsc] .muted { color: #A9A79D !important; }
    [data-ogsc] .accent-text { color: #D4FA5A !important; }
""" + "\n";

    // ---- Header ------------------------------------------------------------------------------------------------

    /// <summary>Luma's mark and wordmark. Without a hosted mark (<c>Email:LogoUrl</c> empty) only the wordmark is
    /// shown. <paramref name="badge"/> adds the small pill (e.g. "Admin").</summary>
    public static string LumaHeader(string? logoUrl, string? badge = null)
    {
        var mark = string.IsNullOrWhiteSpace(logoUrl)
            ? string.Empty
            : $"""<td width="28" valign="middle" style="width:28px;"><img src="{E(logoUrl)}" width="28" height="28" alt="" style="display:block; width:28px; height:28px; border:0; border-radius:8px;"></td>""";
        var wordmarkPadding = mark.Length == 0 ? "" : "padding-left:10px; ";
        var badgeCell = badge is null
            ? string.Empty
            : $"""<td style="padding-left:10px;"><span style="display:inline-block; padding:2px 8px; border-radius:999px; background-color:#17160F; color:#FAFAF6; font-family:{BodyFont}; font-size:11px; font-weight:600; line-height:18px;">{E(badge)}</span></td>""";

        return $"""
            <tr><td class="px" style="padding:0 8px 20px;"><table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                          {mark}
                          <td class="text" style="{wordmarkPadding}font-family:{SerifFont}; font-size:26px; line-height:28px; color:#17160F;">Luma</td>{badgeCell}
                        </tr></table></td></tr>
            """;
    }

    /// <summary>A creator's header: their logo, or a monogram tile in the brand colour, then the brand name.</summary>
    public static string CreatorHeader(string brandName, string? logoUrl, BrandPalette palette)
    {
        var tile = string.IsNullOrWhiteSpace(logoUrl)
            ? $"""<td width="36" height="36" align="center" valign="middle" bgcolor="{palette.Fill}" style="width:36px; height:36px; border-radius:10px; background-color:{palette.Fill}; font-family:{SerifFont}; font-style:italic; font-size:16px; line-height:36px; color:{palette.OnFill};">{E(Monogram(brandName))}</td>"""
            : $"""<td width="36" height="36" valign="middle" style="width:36px; height:36px;"><img src="{E(logoUrl)}" width="36" height="36" alt="" style="display:block; width:36px; height:36px; border:0; border-radius:10px;"></td>""";

        return $"""
            <tr><td class="px" style="padding:0 8px 20px;"><table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                          {tile}
                          <td class="text" style="padding-left:12px; font-family:{SerifFont}; font-size:24px; line-height:28px; color:#17160F;">{E(brandName)}</td>
                        </tr></table></td></tr>
            """;
    }

    /// <summary>"MH Studio" → "MH"; "luma" → "L".</summary>
    public static string Monogram(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Take(2).Select(word => char.ToUpperInvariant(word[0]));
        var monogram = new string(letters.ToArray());
        return monogram.Length == 0 ? "·" : monogram;
    }

    // ---- Card content ------------------------------------------------------------------------------------------

    /// <summary>"Confirm your <em>email.</em>" — <paramref name="emphasis"/> in italics, Luma's accent or a brand class.</summary>
    public static string Heading(string text, string emphasis, string emphasisClass = "accent-text", string emphasisColor = "#4A6A05") =>
        $"""<h1 class="h1 text" style="margin:0 0 20px; font-family:{SerifFont}; font-size:40px; font-weight:400; line-height:44px; letter-spacing:-0.5px; color:#17160F;">{E(text)}<em class="{emphasisClass}" style="font-style:italic; color:{emphasisColor};">{E(emphasis)}</em></h1>""";

    public static string Paragraph(string html) =>
        $"""<p class="text" style="margin:0 0 16px; font-family:{BodyFont}; font-size:16px; line-height:25px; color:#17160F;">{html}</p>""";

    /// <summary>"Hi Marko," — "Hi," without a name.</summary>
    public static string Greeting(string? firstName) =>
        Paragraph(string.IsNullOrWhiteSpace(firstName) ? "Hi," : $"Hi {E(firstName.Trim())},");

    public static string Strong(string text) => $"<strong>{E(text)}</strong>";

    /// <summary>The bulletproof button: Luma's ink button, or a brand-coloured one.</summary>
    public static string Button(string label, string url, BrandPalette? brand = null)
    {
        var (cellClass, fill, textColor) = brand is null ? ("btn-ink", "#17160F", "#FAFAF6") : ("", brand.Fill, brand.OnFill);
        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:28px 0;"><tr>
                              <td class="{cellClass}" align="center" bgcolor="{fill}" style="border-radius:10px; background-color:{fill};">
                                <a href="{E(url)}" target="_blank" style="display:inline-block; padding:14px 28px; border-radius:10px; font-family:{BodyFont}; font-size:15px; font-weight:600; line-height:20px; color:{textColor}; text-decoration:none;">{E(label)}</a>
                              </td>
                            </tr></table>
            """;
    }

    /// <summary>"If the button doesn't work, paste this link…" with the link itself.</summary>
    public static string FallbackLink(string url) =>
        $"""<p class="muted" style="margin:0 0 6px; font-family:{BodyFont}; font-size:13px; line-height:20px; color:#6B695F;">If the button doesn’t work, paste this link into your browser:</p><p style="margin:0 0 4px; font-family:{MonoFont}; font-size:12px; line-height:18px; word-break:break-all;"><a class="accent-text" href="{E(url)}" style="color:#4A6A05;">{E(url)}</a></p>""";

    /// <summary>The small muted closing line ("The link works for 24 hours…").</summary>
    public static string Note(string text, string margin = "20px 0 0") =>
        $"""<p class="muted" style="margin:{margin}; font-family:{BodyFont}; font-size:13px; line-height:20px; color:#6B695F;">{E(text)}</p>""";

    /// <summary>Label / value rows. Values are text, or <see cref="MonoValue"/> markup when <c>IsHtml</c>.</summary>
    public static string KeyValues(params (string Label, string Value, bool IsHtml)[] rows)
    {
        var html = new StringBuilder();
        html.Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 4px;">""");
        foreach (var (label, value, isHtml) in rows)
        {
            html.Append($"""
                <tr>
                                    <td class="muted line" style="padding:10px 0; border-bottom:1px solid #E4E2D9; font-family:{BodyFont}; font-size:14px; line-height:20px; color:#6B695F; width:42%;">{E(label)}</td>
                                    <td class="text line" style="padding:10px 0; border-bottom:1px solid #E4E2D9; font-family:{BodyFont}; font-size:14px; line-height:20px; font-weight:600; color:#17160F; text-align:right;">{(isHtml ? value : E(value))}</td>
                                  </tr>
                """);
        }
        html.Append("</table>");
        return html.ToString();
    }

    public static (string Label, string Value, bool IsHtml) Row(string label, string value) => (label, value, false);

    public static string MonoValue(string text) =>
        $"""<span style="font-family:{MonoFont}; font-weight:400;">{E(text)}</span>""";

    /// <summary>The amber "Wasn't you?" box.</summary>
    public static string Warning(string title, string html) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 0;"><tr>
                          <td class="warn" style="padding:16px 18px; border-radius:12px; background-color:#FBF0DA; font-family:{BodyFont}; font-size:14px; line-height:22px;">
                            <strong class="warn-text" style="color:#9A5606;">{E(title)}</strong><br><span class="text" style="color:#17160F;">{html}</span>
                          </td>
                        </tr></table>
        """;

    // ---- Footer ------------------------------------------------------------------------------------------------

    /// <summary>Luma's footer. Help / Privacy appear only when their URLs are configured.</summary>
    public static string LumaFooter(EmailBranding branding, string? extraLineHtml = null)
    {
        var links = new List<string>();
        if (!string.IsNullOrWhiteSpace(branding.HelpUrl))
            links.Add(FooterLink("Help", branding.HelpUrl));
        if (!string.IsNullOrWhiteSpace(branding.PrivacyUrl))
            links.Add(FooterLink("Privacy", branding.PrivacyUrl));
        links.Add("Made on the Adriatic");

        var footer = "Luma · Landing pages, checkout, email and payouts for creators<br>" + string.Join(" · ", links);
        return extraLineHtml is null ? footer : footer + "<br>" + extraLineHtml;
    }

    public static string FooterLink(string label, string url) =>
        $"""<a class="muted" href="{E(url)}" style="color:#6B695F; text-decoration:underline;">{E(label)}</a>""";

    // ---- Formatting --------------------------------------------------------------------------------------------

    /// <summary>"29 Sep 2026".</summary>
    public static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"30 Sep 2026, 09:14 UTC".</summary>
    public static string DateTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
