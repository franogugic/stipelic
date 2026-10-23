using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>Which URLs of a campaign email become tracked links, and where a URL ends.</summary>
public class CampaignLinkExtractorTests
{
    private static List<string> Urls(string line) =>
        CampaignLinkExtractor.Segment(line).Where(s => s.IsUrl).Select(s => s.Text).ToList();

    [Theory]
    [InlineData("Read https://acme.test/post.", "https://acme.test/post")]
    [InlineData("Read https://acme.test/post, then reply!", "https://acme.test/post")]
    [InlineData("Really? https://acme.test/q?a=1&b=2?", "https://acme.test/q?a=1&b=2")]
    [InlineData("(see https://acme.test/a)", "https://acme.test/a")]
    [InlineData("https://en.wikipedia.test/wiki/Foo_(bar)", "https://en.wikipedia.test/wiki/Foo_(bar)")]
    [InlineData("<https://acme.test/x>", "https://acme.test/x")]
    [InlineData("\"https://acme.test/quoted\"", "https://acme.test/quoted")]
    [InlineData("HTTP://ACME.TEST/upper", "HTTP://ACME.TEST/upper")]
    public void AUrl_EndsBeforeTrailingPunctuationAndUnpairedBrackets(string line, string expected) =>
        Assert.Equal([expected], Urls(line));

    [Theory]
    [InlineData("ftp://acme.test/file")]
    [InlineData("acme.test/no-scheme")]
    [InlineData("mailto:hi@acme.test")]
    [InlineData("https://")]
    [InlineData("javascript:alert(1)")]
    public void OnlyAbsoluteHttpUrls_AreLinks(string line) => Assert.Empty(Urls(line));

    [Fact]
    public void SegmentsGiveTheLineBack()
    {
        const string line = "Hi! See https://a.test/1 and https://b.test/2.";

        var segments = CampaignLinkExtractor.Segment(line);

        Assert.Equal(line, string.Concat(segments.Select(s => s.Text)));
        Assert.Equal(["https://a.test/1", "https://b.test/2"], segments.Where(s => s.IsUrl).Select(s => s.Text));
    }

    [Fact]
    public void LinksAreDistinct_InOrderOfFirstAppearance_WithTheButtonLast()
    {
        var links = CampaignLinkExtractor.Extract(
            ["Start at https://b.test and https://a.test", "Again: https://b.test"], "Buy now", "https://shop.test/buy");

        Assert.Equal(
        [
            new ExtractedLink(1, "https://b.test", CampaignLinkKind.Text, null),
            new ExtractedLink(2, "https://a.test", CampaignLinkKind.Text, null),
            new ExtractedLink(3, "https://shop.test/buy", CampaignLinkKind.Button, "Buy now")
        ], links);
    }

    [Fact]
    public void TheButtonsUrlInTheText_IsOneLink_TheButton_AtItsFirstPosition()
    {
        var links = CampaignLinkExtractor.Extract(["Shop: https://shop.test/buy"], "Buy", "https://shop.test/buy");

        Assert.Equal([new ExtractedLink(1, "https://shop.test/buy", CampaignLinkKind.Button, "Buy")], links);
    }

    [Fact]
    public void NoUrlsAndNoButton_NoLinks() =>
        Assert.Empty(CampaignLinkExtractor.Extract(["Just text."], null, null));
}
