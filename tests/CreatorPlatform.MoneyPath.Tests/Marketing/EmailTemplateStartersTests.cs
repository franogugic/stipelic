using CreatorPlatform.Marketing.Application.Templates;
using CreatorPlatform.Marketing.Domain.Templates;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class EmailTemplateStartersTests
{
    [Fact]
    public void All_AreThePrototypesFourStarters_InOrder()
    {
        Assert.Equal(["announcement", "newsletter", "promotion", "welcome"], EmailTemplateStarters.All.Select(t => t.Key));
        Assert.Equal(["Announcement", "Newsletter", "Promotion", "Welcome"], EmailTemplateStarters.All.Select(t => t.Name));
    }

    [Fact]
    public void All_EveryStarterHasADescription_AndNoButtonUrl()
    {
        Assert.All(EmailTemplateStarters.All, starter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(starter.Description));
            Assert.Null(starter.CtaUrl);
        });
        Assert.Equal(["Learn more", null, "Get the offer", null], EmailTemplateStarters.All.Select(t => t.CtaLabel));
    }

    /// <summary>A template created from a starter (with the button URL the creator fills in) is valid.</summary>
    [Theory]
    [InlineData("announcement")]
    [InlineData("newsletter")]
    [InlineData("promotion")]
    [InlineData("welcome")]
    public void CreatingATemplateFromAStarter_Works(string key)
    {
        var starter = EmailTemplateStarters.All.Single(t => t.Key == key);
        var url = starter.CtaLabel is null ? null : "https://example.test/offer";

        var template = EmailTemplate.Create(1, starter.Name, starter.Subject, starter.BodyText, starter.CtaLabel, url, DateTimeOffset.UtcNow);

        Assert.Equal((starter.Subject, starter.BodyText, starter.CtaLabel), (template.Subject, template.BodyText, template.CtaLabel));
    }

    [Fact]
    public void AStarterButtonWithoutItsUrl_IsNotSaved()
    {
        var starter = EmailTemplateStarters.All.Single(t => t.Key == "promotion");

        Assert.Throws<ArgumentException>(() =>
            EmailTemplate.Create(1, starter.Name, starter.Subject, starter.BodyText, starter.CtaLabel, starter.CtaUrl, DateTimeOffset.UtcNow));
    }
}
