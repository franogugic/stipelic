using System.Text.RegularExpressions;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Infrastructure.Services;

namespace CreatorPlatform.MoneyPath.Tests.Email;

/// <summary>The system emails in the Luma design (designer-prototype/emails): every value encoded, creator branding,
/// the campaign placeholders, and nothing left over from the prototype's sample content.</summary>
public class SystemEmailTemplateTests
{
    private const string Hostile = "<script>alert(\"x\")</script>";
    private const string HostileUrl = "https://app.test/x?a=\"><script>alert(1)</script>";
    private static readonly DateTimeOffset At = new(2026, 10, 6, 9, 14, 0, TimeSpan.Zero);

    private static readonly EmailBranding Branding = new(
        "https://app.test/email/luma-mark-112.png", "https://app.test/help", "https://app.test/privacy", "support@luma.test");

    private static OrderAccessEmail Order(string text = "Jane Doe", string url = "https://api.test/api/access/1", string? brandColor = "#E2553A") => new(
        OrderNumber: "A1B2C3D4",
        BuyerName: text,
        BuyerEmail: "jane@example.test",
        ProductName: text,
        FileLabel: "ZIP · 17.5 MB",
        ProductThumbnailUrl: null,
        AmountCents: 2900,
        Currency: "Eur",
        PaidAt: At,
        DownloadUrl: url,
        CreatorName: text,
        BrandColor: brandColor,
        LogoUrl: null,
        SupportEmail: "help@acme.test");

    private static LeadMagnetEmail Lead(string text = "Jane Doe", string url = "https://app.test/d/1", string? unsubscribeUrl = "https://app.test/unsubscribe/t") => new(
        SignupEmail: "jane@example.test",
        ProductName: text,
        FileLabel: "PDF · 8.2 MB",
        ProductThumbnailUrl: null,
        SignedUpAt: At,
        DownloadUrl: url,
        CreatorName: text,
        BrandColor: "#2F8F9D",
        LogoUrl: null,
        SupportEmail: "help@acme.test",
        UnsubscribeUrl: unsubscribeUrl is null ? null : url);

    /// <summary>Every template, rendered with <paramref name="text"/> in every text slot and <paramref name="url"/>
    /// in every URL slot.</summary>
    private static IEnumerable<(string Name, RenderedEmail Email)> All(string text, string url)
    {
        yield return ("verify", EmailVerificationTemplate.Render(Branding, text, text, url));
        yield return ("reset", PasswordResetTemplate.Render(Branding, text, text, url));
        yield return ("change-confirm", EmailChangeVerificationTemplate.Render(Branding, text, text, text, url));
        yield return ("change-notice", EmailChangedTemplate.Render(Branding, text, text, text, At, url));
        yield return ("order", OrderAccessTemplate.Render(Order(text, url)));
        yield return ("lead-magnet", LeadMagnetTemplate.Render(Lead(text, url)));
        var campaign = new CampaignEmailRenderer().Render(text, text, text, url, text, null, "#E2553A", text);
        yield return ("campaign", new RenderedEmail(campaign.Subject, campaign.HtmlBody, campaign.PlainTextBody));
    }

    [Fact]
    public void EveryTemplate_EncodesScriptAndQuotesInEveryValue()
    {
        foreach (var (name, email) in All(Hostile, HostileUrl))
        {
            Assert.False(email.Html.Contains("<script", StringComparison.OrdinalIgnoreCase), $"{name}: raw <script> in the HTML");
            Assert.Contains("&lt;script&gt;", email.Html);
            // A value can't break out of an attribute either: its quotes arrive encoded.
            Assert.DoesNotContain("alert(\"x\")", email.Html);
            Assert.Contains("&quot;", email.Html);
        }
    }

    [Fact]
    public void NoEmail_LoadsAStylesheetOrFontFromAnotherHost()
    {
        // S15/QA-14: a Google Fonts link costs a third-party request per open, leaks the open, and is blocked by many
        // clients. The font stacks fall back to system fonts.
        foreach (var (name, email) in All("Jane Doe", "https://app.test/x"))
        {
            Assert.DoesNotContain("<link", email.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fonts.googleapis.com", email.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fonts.gstatic.com", email.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@import", email.Html, StringComparison.OrdinalIgnoreCase);
            Assert.True(email.Html.Contains("Helvetica, Arial, sans-serif", StringComparison.Ordinal), name);
        }
    }

    [Fact]
    public void UrlsEndUpEncodedInsideTheHref()
    {
        var email = EmailVerificationTemplate.Render(Branding, "Jane", "jane@example.test", HostileUrl);

        Assert.Contains("href=\"https://app.test/x?a=&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;\"", email.Html);
    }

    [Fact]
    public void TheOrderEmail_LinksThePersonalDownloadPage_WithTheFileLabel()
    {
        var email = OrderAccessTemplate.Render(Order("Adriatic Summer Presets", "https://app.test/d/tok"));

        Assert.Equal("Your Adriatic Summer Presets", email.Subject);
        Assert.Contains("Download your file", email.Html);
        Assert.Contains("href=\"https://app.test/d/tok\"", email.Html);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("ZIP · 17.5 MB"), email.Html);
        Assert.Contains("It’s personal, so please don’t share it.", email.Html);
        Assert.Contains("https://app.test/d/tok", email.PlainText);
        Assert.DoesNotContain("/api/access/", email.Html);
    }

    [Fact]
    public void TheLeadMagnetEmail_LinksTheDownload_AndOffersUnsubscribeOnlyToSubscribers()
    {
        var subscribed = LeadMagnetTemplate.Render(Lead("Color Grading Guide", "https://app.test/d/tok"));
        var unsubscribed = LeadMagnetTemplate.Render(Lead("Color Grading Guide", "https://app.test/d/tok", unsubscribeUrl: null));

        Assert.Equal("Your free download: Color Grading Guide", subscribed.Subject);
        Assert.Contains("Download the guide", subscribed.Html);
        Assert.Contains("href=\"https://app.test/d/tok\"", subscribed.Html);
        Assert.Contains(System.Net.WebUtility.HtmlEncode("PDF · 8.2 MB"), subscribed.Html);
        Assert.Contains(">Free<", subscribed.Html);
        Assert.Contains("Unsubscribe", subscribed.Html);
        Assert.Contains("You’ll also get", subscribed.PlainText);

        Assert.DoesNotContain("Unsubscribe", unsubscribed.Html);
        Assert.DoesNotContain("You’ll also get", unsubscribed.PlainText);
        Assert.Contains("https://app.test/d/tok", unsubscribed.PlainText);
    }

    [Fact]
    public void TheFileLabel_NamesTheKindAndSize()
    {
        Assert.Equal("PDF · 8.2 MB", EmailFileLabel.From("application/pdf", 8_600_000));
        Assert.Equal("ZIP · 17.5 MB", EmailFileLabel.From("application/zip", 18_350_080));
        Assert.Equal("EPUB · 100 MB", EmailFileLabel.From("application/epub+zip", 104_857_600));
        Assert.Equal("PDF · 640 KB", EmailFileLabel.From("application/pdf", 655_360));
        Assert.Null(EmailFileLabel.From(null, null));
    }

    [Fact]
    public void TheOrderEmail_UsesTheCreatorsBrand_WithTextShadedForContrast()
    {
        var palette = BrandPalette.From("#E2553A");
        var html = OrderAccessTemplate.Render(Order()).Html;

        Assert.Contains("bgcolor=\"#E2553A\"", html);                               // button + monogram tile fill
        Assert.Contains($"color:{palette.TextLight};", html);                       // heading emphasis, support link
        Assert.Contains($".brand-text {{ color: {palette.TextDark} !important; }}", html);
        Assert.True(Contrast(palette.TextLight, "#FFFFFF") >= 4.5);
        Assert.True(Contrast(palette.TextDark, "#181816") >= 4.5);
        Assert.Contains(">JD<", html);                                              // monogram of "Jane Doe"
        Assert.Contains("mailto:help@acme.test", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-colour")]
    public void TheOrderEmail_FallsBackToLumasAccent_WithoutABrandColour(string? brandColor)
    {
        var html = OrderAccessTemplate.Render(Order(brandColor: brandColor)).Html;

        Assert.Contains($"bgcolor=\"{BrandPalette.LumaAccent}\"", html);
        Assert.Contains($"color:{BrandPalette.Luma.TextLight};", html);
    }

    [Fact]
    public void TheCampaignLayout_KeepsTheUnsubscribePlaceholderAndThePixel()
    {
        var content = new CampaignEmailRenderer().Render(
            "Monthly notes", "First line.\nSecond line.", "Read more", "https://acme.test/notes", "Acme", null, "#4F74D9", "hello@acme.test");

        Assert.Contains("href=\"{{UNSUBSCRIBE_URL}}\"", content.HtmlBody);
        Assert.Contains("<img src=\"{{OPEN_PIXEL_URL}}\" width=\"1\" height=\"1\"", content.HtmlBody);
        Assert.EndsWith("</html>", content.HtmlBody.TrimEnd());
        Assert.Contains("Unsubscribe: {{UNSUBSCRIBE_URL}}", content.PlainTextBody);
        Assert.DoesNotContain("OPEN_PIXEL_URL", content.PlainTextBody);
        Assert.Contains("bgcolor=\"#4F74D9\"", content.HtmlBody);
        Assert.Contains("Reply to hello@acme.test", content.HtmlBody);
        Assert.Contains("First line.", content.PlainTextBody);
    }

    [Fact]
    public void TheCampaignFooter_SaysWhyTheRecipientGetsIt_ByAudience()
    {
        var renderer = new CampaignEmailRenderer();

        var signedUp = renderer.Render("Hi", "Body.", null, null, "Acme", null, "#4F74D9", "hello@acme.test");
        var bought = renderer.Render("Hi", "Body.", null, null, "Acme", null, "#4F74D9", "hello@acme.test", CampaignAudienceReason.Bought);

        Assert.Contains("You’re receiving this because you signed up at Acme.", signedUp.HtmlBody);
        Assert.Contains("You’re receiving this because you signed up at Acme.", signedUp.PlainTextBody);
        Assert.DoesNotContain("you bought from", signedUp.HtmlBody);
        Assert.Contains("You’re receiving this because you bought from Acme.", bought.HtmlBody);
        Assert.Contains("You’re receiving this because you bought from Acme.", bought.PlainTextBody);
        Assert.DoesNotContain("you signed up at", bought.HtmlBody);
        Assert.DoesNotContain("you signed up at", bought.PlainTextBody);
        // The unsubscribe link stays on both.
        Assert.Contains("href=\"{{UNSUBSCRIBE_URL}}\"", bought.HtmlBody);
        Assert.Contains("Unsubscribe: {{UNSUBSCRIBE_URL}}", bought.PlainTextBody);
    }

    [Fact]
    public void TheBuyersFooter_EncodesTheBrandName()
    {
        var bought = new CampaignEmailRenderer().Render(
            "Hi", "Body.", null, null, "<b>Acme</b> & Co", null, "#4F74D9", null, CampaignAudienceReason.Bought);

        Assert.Contains("you bought from &lt;b&gt;Acme&lt;/b&gt; &amp; Co.", bought.HtmlBody);
        Assert.DoesNotContain("<b>Acme</b> & Co.<br>", bought.HtmlBody);
    }

    [Fact]
    public void NoTemplate_KeepsThePrototypesSampleContent()
    {
        var samples = new[] { @"\bAna\b", @"\bMarko\b", "MH Studio", "mhstudio.hr", "Adriatic Summer", "luma.app/", "#1072", "Kovačević", "Horvat" };

        foreach (var (name, email) in All("Jane Doe", "https://app.test/link"))
        {
            foreach (var sample in samples)
            {
                Assert.False(Regex.IsMatch(email.Html, sample), $"{name}: HTML still contains sample text {sample}");
                Assert.False(Regex.IsMatch(email.PlainText, sample), $"{name}: plain text still contains sample text {sample}");
            }
            Assert.False(string.IsNullOrWhiteSpace(email.PlainText), $"{name}: no plain-text part");
        }
    }

    [Fact]
    public void EveryTemplate_IsTheEmailSafeLayout()
    {
        foreach (var (name, email) in All("Jane Doe", "https://app.test/link"))
        {
            Assert.Contains("width=\"600\"", email.Html);
            Assert.Contains("@media (prefers-color-scheme: dark)", email.Html);
            Assert.Contains("[data-ogsc]", email.Html);
            Assert.Contains("mso-hide:all;", email.Html);           // hidden preheader
            Assert.DoesNotContain("__BRAND_TEXT_DARK__", email.Html);
        }
    }

    [Fact]
    public void TheLumaMark_IsShownWhenConfigured_AndFallsBackToTheWordmark()
    {
        var withMark = PasswordResetTemplate.Render(Branding, "Jane", "jane@example.test", "https://app.test/r").Html;
        var withoutMark = PasswordResetTemplate.Render(EmailBranding.None, "Jane", "jane@example.test", "https://app.test/r").Html;

        Assert.Contains("<img src=\"https://app.test/email/luma-mark-112.png\"", withMark);
        Assert.DoesNotContain("<img", withoutMark);
        Assert.Contains(">Luma</td>", withoutMark);
        // Footer links and the support address appear only when configured.
        Assert.Contains("https://app.test/privacy", withMark);
        Assert.DoesNotContain("Privacy</a>", withoutMark);
    }

    [Theory]
    [InlineData(2900, "Eur", "€29.00")]
    [InlineData(123456, "USD", "$1,234.56")]
    [InlineData(150000, "rsd", "1,500.00 RSD")]
    public void Money_IsFormattedForEmails(long cents, string currency, string expected)
    {
        Assert.Equal(expected, EmailMoney.Format(cents, currency));
    }

    private static double Contrast(string a, string b)
    {
        static double Luminance(string hex)
        {
            double Channel(int i)
            {
                var c = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
        }
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
