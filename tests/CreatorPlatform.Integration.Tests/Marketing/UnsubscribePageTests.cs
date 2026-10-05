using CreatorPlatform.Api.Controllers;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>The unsubscribe flow behind the creator-branded page: reading never unsubscribes, confirming does (once),
/// the RFC 8058 one-click POST still works, and old GET links only redirect to the page.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UnsubscribePageTests
{
    private const string FrontendBaseUrl = "https://app.luma.test";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly UnsubscribeTokenService _tokens = new(Options.Create(new MarketingOptions
    {
        UnsubscribeTokenSecret = "integration-test-unsubscribe-secret",
        ApiBaseUrl = "https://api.luma.test",
        FrontendBaseUrl = FrontendBaseUrl,
    }));

    public UnsubscribePageTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithControllerAsync<T>(Func<PublicUnsubscribeController, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        var repository = new UnsubscribeRepository(db);
        var pages = new UnsubscribePageService(_tokens, repository, new MarketingCreatorContextProvider(db));
        return await act(new PublicUnsubscribeController(_tokens, repository, pages));
    }

    private static UnsubscribePageDto Body(ActionResult<ApiResponse<UnsubscribePageDto>> result) =>
        Assert.IsType<ApiResponse<UnsubscribePageDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).Data!;

    [Fact]
    public async Task Info_ShowsTheCreatorsBrand_AndHasNoSideEffect()
    {
        var creator = await _data.CreateCreatorAsync();
        await AddSettingsAsync(creator.CreatorId, "MH Studio", "#4F74D9", "https://cdn.example.test/logo.png", "hello@mh.test");
        var email = TestData.UniqueEmail();
        var token = _tokens.Create(creator.CreatorId, email);

        var page = Body(await WithControllerAsync(c => c.Info(token, CancellationToken.None)));
        await WithControllerAsync(c => c.Info(token, CancellationToken.None));

        Assert.Equal(new UnsubscribePageCreatorDto("MH Studio", "#4F74D9", "https://cdn.example.test/logo.png", "hello@mh.test"), page.Creator);
        Assert.False(page.AlreadyUnsubscribed);
        Assert.False(await _data.UnsubscribeExistsAsync(creator.CreatorId, email));
    }

    [Fact]
    public async Task Confirm_UnsubscribesOnce_AndIsIdempotent()
    {
        var creator = await _data.CreateCreatorAsync();
        var email = TestData.UniqueEmail();
        var token = _tokens.Create(creator.CreatorId, email);

        var first = Body(await WithControllerAsync(c => c.Confirm(token, CancellationToken.None)));
        var second = Body(await WithControllerAsync(c => c.Confirm(token, CancellationToken.None)));

        Assert.True(first.AlreadyUnsubscribed);
        Assert.True(second.AlreadyUnsubscribed);
        Assert.Equal(1, await CountUnsubscribesAsync(creator.CreatorId, email));
        Assert.True(Body(await WithControllerAsync(c => c.Info(token, CancellationToken.None))).AlreadyUnsubscribed);
        // Without brand settings: the workspace name.
        Assert.Equal("Test Creator", first.Creator.Name);
    }

    [Fact]
    public async Task TheOneClickPost_StillUnsubscribes()
    {
        var creator = await _data.CreateCreatorAsync();
        var email = TestData.UniqueEmail();
        var token = _tokens.Create(creator.CreatorId, email);

        var result = await WithControllerAsync(c => c.OneClick(token, CancellationToken.None));

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, await CountUnsubscribesAsync(creator.CreatorId, email));
    }

    [Fact]
    public async Task TheOldGetLink_RedirectsToThePage_WithoutUnsubscribing()
    {
        var creator = await _data.CreateCreatorAsync();
        var email = TestData.UniqueEmail();
        var token = _tokens.Create(creator.CreatorId, email);

        await using var db = _fixture.CreateDbContext();
        var repository = new UnsubscribeRepository(db);
        var controller = new PublicUnsubscribeController(
            _tokens, repository, new UnsubscribePageService(_tokens, repository, new MarketingCreatorContextProvider(db)));

        var redirect = controller.LegacyLink(token);

        Assert.Equal($"{FrontendBaseUrl}/unsubscribe/{token}", redirect.Url);
        Assert.False(redirect.Permanent);   // 302
        Assert.False(await _data.UnsubscribeExistsAsync(creator.CreatorId, email));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("tampered")]
    public async Task AnInvalidToken_Is404WithItsCode(string kind)
    {
        var creator = await _data.CreateCreatorAsync();
        var valid = _tokens.Create(creator.CreatorId, TestData.UniqueEmail());
        var token = kind == "tampered" ? valid[..^2] + (valid[^1] == 'A' ? "BB" : "AA") : kind;

        var info = await Assert.ThrowsAsync<NotFoundException>(() => WithControllerAsync(c => c.Info(token, CancellationToken.None)));
        var confirm = await Assert.ThrowsAsync<NotFoundException>(() => WithControllerAsync(c => c.Confirm(token, CancellationToken.None)));

        Assert.Equal(("unsubscribe_link_invalid", "unsubscribe_link_invalid"), (info.Code, confirm.Code));
    }

    [Fact]
    public async Task ALinkOfADeletedWorkspace_Is404()
    {
        var creator = await _data.CreateCreatorAsync();
        var token = _tokens.Create(creator.CreatorId, TestData.UniqueEmail());
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""UPDATE creators.creators SET "Status" = 'Disabled' WHERE "Id" = {creator.CreatorId}""");

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => WithControllerAsync(c => c.Info(token, CancellationToken.None)));
        Assert.Equal("unsubscribe_link_invalid", exception.Code);
    }

    private async Task AddSettingsAsync(int creatorId, string brandName, string color, string? logoUrl, string? supportEmail)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "SupportEmail", "BrandName", "LogoUrl", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, {supportEmail}, {brandName}, {logoUrl}, {color}, 'Europe/Zagreb', 'en', {now}, {now})
            """);
    }

    private async Task<int> CountUnsubscribesAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM marketing.unsubscribes WHERE "CreatorId" = {creatorId} AND "Email" = {email}
            """).ToListAsync()).Single();
    }
}
