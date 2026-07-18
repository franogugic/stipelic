using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Unsubscribes;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignRecipientAndUnsubscribeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CampaignRecipient_Create_NormalizesEmail()
    {
        var recipient = CampaignRecipient.Create(1, "  Someone@Example.COM  ", Now);

        Assert.Equal("someone@example.com", recipient.Email);
    }

    [Fact]
    public void CampaignRecipient_Create_EmptyEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => CampaignRecipient.Create(1, "  ", Now));
    }

    [Fact]
    public void Unsubscribe_Create_NormalizesEmail()
    {
        var unsubscribe = Unsubscribe.Create(1, "  Someone@Example.COM  ", UnsubscribeSource.Link, Now);

        Assert.Equal("someone@example.com", unsubscribe.Email);
        Assert.Equal(UnsubscribeSource.Link, unsubscribe.Source);
    }

    [Fact]
    public void Unsubscribe_Create_EmptyEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => Unsubscribe.Create(1, "", UnsubscribeSource.OneClick, Now));
    }
}
