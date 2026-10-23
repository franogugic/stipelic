using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Services;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>The rendered email links every body URL and the button to per-render placeholders, in HTML and plain
/// text, and leaves the unsubscribe link and the pixel alone.</summary>
public class CampaignEmailRendererLinkTests
{
    private static readonly CampaignEmailRenderer Renderer = new();

    [Fact]
    public void BodyUrlsAndTheButton_PointAtTheirPlaceholders_InBothBodies()
    {
        var content = Renderer.Render(
            "Hi", "Read https://blog.test/post.\nThen https://blog.test/post again.", "Buy", "https://shop.test/buy",
            "Acme", null, "#E2553A");

        Assert.Equal(2, content.Links.Count);
        var (text, button) = (content.Links[0], content.Links[1]);
        Assert.Equal((1, "https://blog.test/post", CampaignLinkKind.Text, (string?)null), (text.Position, text.Url, text.Kind, text.Label));
        Assert.Equal((2, "https://shop.test/buy", CampaignLinkKind.Button, "Buy"), (button.Position, button.Url, button.Kind, button.Label));

        Assert.Contains($"""<a href="{text.Placeholder}" target="_blank" """, content.HtmlBody);
        Assert.Contains(">https://blog.test/post</a>.", content.HtmlBody);
        Assert.Contains($"""href="{button.Placeholder}" """, content.HtmlBody);
        Assert.DoesNotContain("href=\"https://blog.test", content.HtmlBody);
        Assert.DoesNotContain("href=\"https://shop.test", content.HtmlBody);

        Assert.Contains($"Read {text.Placeholder}.", content.PlainTextBody);
        Assert.Contains($"Then {text.Placeholder} again.", content.PlainTextBody);
        Assert.Contains($"Buy: {button.Placeholder}", content.PlainTextBody);
        Assert.DoesNotContain("https://blog.test", content.PlainTextBody);
        Assert.DoesNotContain("https://shop.test", content.PlainTextBody);
    }

    [Fact]
    public void TheUnsubscribeLinkAndThePixel_AreNotCampaignLinks()
    {
        var content = Renderer.Render("Hi", "No links here.", null, null, "Acme", null, "#E2553A");

        Assert.Empty(content.Links);
        Assert.Contains("href=\"{{UNSUBSCRIBE_URL}}\"", content.HtmlBody);
        Assert.Contains("src=\"{{OPEN_PIXEL_URL}}\"", content.HtmlBody);
        Assert.Contains("Unsubscribe: {{UNSUBSCRIBE_URL}}", content.PlainTextBody);
    }

    [Fact]
    public void PlaceholdersAreFreshPerRender_SoTypedTextCannotCollide()
    {
        var first = Renderer.Render("Hi", "https://a.test", null, null, "Acme", null, "#E2553A");
        var typed = first.Links[0].Placeholder;

        var second = Renderer.Render("Hi", $"https://a.test {typed}", null, null, "Acme", null, "#E2553A");

        Assert.NotEqual(typed, second.Links[0].Placeholder);
        Assert.Contains(typed, second.PlainTextBody);
    }

    [Fact]
    public void LinkTextIsHtmlEncoded()
    {
        var content = Renderer.Render("Hi", "Go https://a.test/?q=<b>&x=1", null, null, "Acme", null, "#E2553A");

        Assert.Equal("https://a.test/?q=", content.Links[0].Url);
        Assert.Contains("&lt;b&gt;&amp;x=1", content.HtmlBody);
    }
}
