using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignServiceTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;
    private static readonly Guid CreatorPublicId = Guid.NewGuid();
    private static readonly Guid TargetPublicId = Guid.NewGuid();

    private static MarketingCreatorContext BuildContext() =>
        new(CreatorId, CreatorPublicId, "Acme", Slug, "support@acme.test", "Acme", null, "#111111", "owner@acme.test");

    private static (CampaignService Service, FakeMarketingCreatorContextProvider ContextProvider, FakeAudienceService AudienceService, FakeCreatorUsageService UsageService)
        BuildService()
    {
        var contextProvider = new FakeMarketingCreatorContextProvider { Context = BuildContext(), LandingPageId = 10, PlanLimit = 500 };
        var audienceService = new FakeAudienceService { Count = 42 };
        var usageService = new FakeCreatorUsageService();
        var campaignRepository = new FakeCampaignRepository();
        var progressProvider = new FakeCampaignProgressProvider();

        var service = new CampaignService(contextProvider, audienceService, usageService, campaignRepository, progressProvider);

        return (service, contextProvider, audienceService, usageService);
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_UnknownCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.Context = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAudiencePreviewAsync(Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_LandingPageNotOwnedByCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.LandingPageId = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAudiencePreviewAsync(Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_ProductNotOwnedByCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.ProductId = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAudiencePreviewAsync(Slug, OwnerUserId, CampaignAudienceType.Product, TargetPublicId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_ReturnsRecipientCountAndRemaining()
    {
        var (service, _, _, usageService) = BuildService();
        usageService.Used[(CreatorId, "max_email_sends_per_month")] = 150;

        var preview = await service.GetAudiencePreviewAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, CancellationToken.None);

        Assert.Equal(42, preview.RecipientCount);
        Assert.Equal(500, preview.MonthlyLimit);
        Assert.Equal(150, preview.UsedThisMonth);
        Assert.Equal(350, preview.Remaining);
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_UnlimitedPlan_RemainingIsMaxValue()
    {
        var (service, contextProvider, _, usageService) = BuildService();
        contextProvider.PlanLimit = -1;
        usageService.Used[(CreatorId, "max_email_sends_per_month")] = 999_999;

        var preview = await service.GetAudiencePreviewAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, CancellationToken.None);

        Assert.Equal(-1, preview.MonthlyLimit);
        Assert.Equal(int.MaxValue, preview.Remaining);
    }

    [Fact]
    public async Task GetAudiencePreviewAsync_NoActiveSubscription_TreatsLimitAsZero()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.PlanLimit = null;

        var preview = await service.GetAudiencePreviewAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, CancellationToken.None);

        Assert.Equal(0, preview.MonthlyLimit);
        Assert.Equal(0, preview.Remaining);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_UnknownCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.Context = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAudienceRecipientsAsync(Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, null, 10, CancellationToken.None));
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_TargetNotOwnedByCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _, _) = BuildService();
        contextProvider.LandingPageId = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetAudienceRecipientsAsync(Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, null, 10, CancellationToken.None));
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_FirstPage_ReturnsExactlyLimitRowsAndHasMoreTrue()
    {
        var (service, _, audienceService, _) = BuildService();
        audienceService.PageableEmails = ["a@x.test", "b@x.test", "c@x.test", "d@x.test", "e@x.test"];

        var page = await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, null, 2, CancellationToken.None);

        Assert.Equal(["a@x.test", "b@x.test"], page.Emails);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_AfterEmail_ContinuesWithoutRepeatingOrSkipping()
    {
        var (service, _, audienceService, _) = BuildService();
        audienceService.PageableEmails = ["a@x.test", "b@x.test", "c@x.test", "d@x.test", "e@x.test"];

        var page = await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, "b@x.test", 2, CancellationToken.None);

        Assert.Equal(["c@x.test", "d@x.test"], page.Emails);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_LastPage_HasMoreFalse()
    {
        var (service, _, audienceService, _) = BuildService();
        audienceService.PageableEmails = ["a@x.test", "b@x.test", "c@x.test"];

        var page = await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, "b@x.test", 2, CancellationToken.None);

        Assert.Equal(["c@x.test"], page.Emails);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_ZeroOrNegativeLimit_ClampsToDefault()
    {
        var (service, _, audienceService, _) = BuildService();
        audienceService.PageableEmails = ["a@x.test"];

        await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, null, 0, CancellationToken.None);

        Assert.Equal(50, audienceService.GetPageCalls[0].Limit);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_LimitAboveMax_ClampsToMax()
    {
        var (service, _, audienceService, _) = BuildService();
        audienceService.PageableEmails = ["a@x.test"];

        await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.LandingPage, TargetPublicId, null, 500, CancellationToken.None);

        Assert.Equal(100, audienceService.GetPageCalls[0].Limit);
    }

    [Fact]
    public async Task GetAudienceRecipientsAsync_ProductAudience_ResolvesProductIdNotLandingPageId()
    {
        var (service, contextProvider, audienceService, _) = BuildService();
        contextProvider.ProductId = 77;
        audienceService.PageableEmails = ["a@x.test"];

        await service.GetAudienceRecipientsAsync(
            Slug, OwnerUserId, CampaignAudienceType.Product, TargetPublicId, null, 10, CancellationToken.None);

        var call = audienceService.GetPageCalls[0];
        Assert.Equal(77, call.ProductId);
        Assert.Null(call.LandingPageId);
    }
}
