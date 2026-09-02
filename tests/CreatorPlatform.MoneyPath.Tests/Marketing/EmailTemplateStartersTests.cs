using CreatorPlatform.Marketing.Application.Templates;
using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class EmailTemplateStartersTests
{
    [Fact]
    public void All_HasFourStarters()
    {
        Assert.Equal(4, EmailTemplateStarters.All.Count);
    }

    [Theory]
    [InlineData("product-launch")]
    [InlineData("welcome-new-subscriber")]
    [InlineData("limited-time-discount")]
    [InlineData("win-back-inactive")]
    public void All_ExpectedKeysPresent(string key)
    {
        Assert.Contains(EmailTemplateStarters.All, t => t.Key == key);
    }

    [Fact]
    public void All_EveryStarter_PassesMailContentRulesValidate()
    {
        foreach (var starter in EmailTemplateStarters.All)
        {
            var exception = Record.Exception(() =>
                MailContentRules.Validate(starter.Subject, starter.BodyText, starter.CtaLabel, starter.CtaUrl));

            Assert.Null(exception);
        }
    }

    [Fact]
    public void All_KeysAreUnique()
    {
        var keys = EmailTemplateStarters.All.Select(t => t.Key).ToList();

        Assert.Equal(keys.Distinct().Count(), keys.Count);
    }
}
