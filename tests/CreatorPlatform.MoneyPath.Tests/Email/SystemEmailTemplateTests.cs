using System.Text.RegularExpressions;
using CreatorPlatform.Email.Application.Templates;
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
        ProductTypeLabel: "Digital download",
        ProductThumbnailUrl: null,
        AmountCents: 2900,
        Currency: "Eur",
        PaidAt: At,
        AccessUrl: url,
        CreatorName: text,
        BrandColor: brandColor,
        LogoUrl: null,
        SupportEmail: "help@acme.test");

    private static PayoutRequestedEmail Payout(string text = "Jane Doe") => new(
        text, text, text, 50000, "Eur", "RS35260005601001611379", "RS", At, 74860);

    /// <summary>Every template, rendered with <paramref name="text"/> in every text slot and <paramref name="url"/>
    /// in every URL slot.</summary>
    private static IEnumerable<(string Name, RenderedEmail Email)> All(string text, string url)
    {
        yield return ("verify", EmailVerificationTemplate.Render(Branding, text, text, url));
        yield return ("reset", PasswordResetTemplate.Render(Branding, text, text, url));
        yield return ("change-confirm", EmailChangeVerificationTemplate.Render(Branding, text, text, text, url));
        yield return ("change-notice", EmailChangedTemplate.Render(Branding, text, text, text, At, url));
        yield return ("payout", PayoutRequestedTemplate.Render(Branding, Payout(text), url));
        yield return ("order", OrderAccessTemplate.Render(Order(text, url)));
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
    public void UrlsEndUpEncodedInsideTheHref()
    {
        var email = EmailVerificationTemplate.Render(Branding, "Jane", "jane@example.test", HostileUrl);

        Assert.Contains("href=\"https://app.test/x?a=&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;\"", email.Html);
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

    [Fact]
    public void ThePayoutEmail_ShowsTheBankDetailsForTheAdmin()
    {
        var html = PayoutRequestedTemplate.Render(Branding, Payout(), "https://app.test/admin/payouts").Html;

        Assert.Contains("€500.00", html);
        Assert.Contains("RS35 2600 0560 1001 6113 79", html);
        Assert.Contains("Serbia", html);
        Assert.Contains("€748.60", html);
        Assert.Contains("6 Oct 2026, 09:14 UTC", html);
        Assert.Contains(">Admin</span>", html);
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
