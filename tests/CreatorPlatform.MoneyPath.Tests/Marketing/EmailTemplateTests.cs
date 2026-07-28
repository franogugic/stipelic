using CreatorPlatform.Marketing.Domain.Mail;
using CreatorPlatform.Marketing.Domain.Templates;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class EmailTemplateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static EmailTemplate CreateValidTemplate() => EmailTemplate.Create(
        creatorId: 1,
        name: "Summer sale",
        subject: "Big sale!",
        bodyText: "Check out our new product.",
        ctaLabel: null,
        ctaUrl: null,
        createdAt: Now);

    [Fact]
    public void Create_Valid_Succeeds()
    {
        var template = CreateValidTemplate();

        Assert.Equal(EmailTemplateStatus.Active, template.Status);
        Assert.Equal("Summer sale", template.Name);
        Assert.Equal("Big sale!", template.Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, name, "Subject", "Body", null, null, Now));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var name = new string('a', EmailTemplate.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, name, "Subject", "Body", null, null, Now));
    }

    // These delegate straight to MailContentRules — same tests as CampaignTests, proving the shared
    // helper is actually shared (not two copies of the same rules that could drift).
    [Fact]
    public void Create_EmptySubject_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, "Name", "", "Body", null, null, Now));
    }

    [Fact]
    public void Create_CtaLabelWithoutUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, "Name", "Subject", "Body", "Buy now", null, Now));
    }

    [Fact]
    public void Create_CtaUrlIsJavascriptScheme_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, "Name", "Subject", "Body", "Buy now", "javascript:alert(1)", Now));
    }

    [Fact]
    public void Create_CtaUrlIsRelative_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailTemplate.Create(
            1, "Name", "Subject", "Body", "Buy now", "/checkout", Now));
    }

    [Fact]
    public void Create_CtaUrlIsHttps_Succeeds()
    {
        var template = EmailTemplate.Create(1, "Name", "Subject", "Body", "Buy now", "https://example.com/buy", Now);

        Assert.Equal("https://example.com/buy", template.CtaUrl);
    }

    [Fact]
    public void Update_WhileActive_Succeeds()
    {
        var template = CreateValidTemplate();

        template.Update("New name", "New subject", "New body", "Buy", "https://example.com", Now);

        Assert.Equal("New name", template.Name);
        Assert.Equal("New subject", template.Subject);
        Assert.Equal("New body", template.BodyText);
        Assert.Equal("Buy", template.CtaLabel);
    }

    [Fact]
    public void Update_AfterArchived_Throws()
    {
        var template = CreateValidTemplate();
        template.Archive(Now);

        Assert.Throws<InvalidOperationException>(() => template.Update(
            "New name", "New subject", "New body", null, null, Now));
    }

    [Fact]
    public void Archive_FromActive_Succeeds()
    {
        var template = CreateValidTemplate();

        template.Archive(Now);

        Assert.Equal(EmailTemplateStatus.Archived, template.Status);
    }

    [Fact]
    public void Archive_AlreadyArchived_Throws()
    {
        var template = CreateValidTemplate();
        template.Archive(Now);

        Assert.Throws<InvalidOperationException>(() => template.Archive(Now));
    }
}
