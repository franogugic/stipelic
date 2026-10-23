using System.Reflection;
using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Api.Controllers;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Application.Analytics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>S2: lead sign-ups are limited per IP (5 / 10 min) and per email address across IPs (5 / 10 min, the address
/// hashed); checkout per IP (10 / 10 min); a filled-in honeypot answers 200 and creates and sends nothing.</summary>
[Collection(RedisCollection.Name)]
public sealed class PublicSignupAndCheckoutLimitsTests
{
    private readonly RedisFixture _redis;

    public PublicSignupAndCheckoutLimitsTests(RedisFixture redis)
    {
        _redis = redis;
    }

    private CaptureEmailRateLimiter Limiter() =>
        new(new RedisRateLimiters(RedisFixture.CreateClient(_redis.CreateOptions()).Client));

    private static (int Limit, TimeSpan Window) Numbers(string policy)
    {
        var definition = Assert.Single(RateLimitPolicies.Catalog, p => p.Name == policy);
        return (definition.PermitLimit, definition.Window);
    }

    private static string? PolicyOf(string action) =>
        typeof(PublicLandingPagesController).GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

    [Fact]
    public void SignUpsAndCheckouts_HaveTheirOwnPolicies()
    {
        Assert.Equal(RateLimitPolicies.CaptureSignup, PolicyOf(nameof(PublicLandingPagesController.Capture)));
        Assert.Equal(RateLimitPolicies.StartCheckout, PolicyOf(nameof(PublicLandingPagesController.Checkout)));
        Assert.Equal((5, TimeSpan.FromMinutes(10)), Numbers(RateLimitPolicies.CaptureSignup));
        Assert.Equal((10, TimeSpan.FromMinutes(10)), Numbers(RateLimitPolicies.StartCheckout));
        Assert.Equal((5, TimeSpan.FromMinutes(10)),
            (RateLimitPolicies.CaptureSignupPerEmail.PermitLimit, RateLimitPolicies.CaptureSignupPerEmail.Window));
    }

    [Fact]
    public async Task OneAddress_GetsFiveSignUps_HoweverItIsWritten_AndAnotherAddressIsNotAffected()
    {
        await using var limiter = Limiter();
        var ana = $"ana-{Guid.NewGuid():N}@example.test";

        for (var i = 0; i < 5; i++)
        {
            using var lease = await limiter.AcquireAsync(i % 2 == 0 ? ana : $"  {ana.ToUpperInvariant()} ", default);
            Assert.True(lease.IsAcquired);
        }

        using var sixth = await limiter.AcquireAsync(ana, default);
        using var other = await limiter.AcquireAsync($"bo-{Guid.NewGuid():N}@example.test", default);
        Assert.False(sixth.IsAcquired);
        Assert.True(other.IsAcquired);
    }

    [Fact]
    public async Task AFilledHoneypot_Answers200_AndCapturesNothing_NorUsesTheAddressesBudget()
    {
        await using var limiter = Limiter();
        var captures = new RecordingCaptures();
        var controller = Controller(limiter, captures);
        var email = $"ana-{Guid.NewGuid():N}@example.test";

        for (var i = 0; i < 6; i++)
        {
            var result = await controller.Capture("acme", "guide", new EmailCaptureRequestDto { Email = email, Website = "http://spam.example" }, default);
            Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result.Result!).StatusCode);
        }

        Assert.Empty(captures.Emails);
        await controller.Capture("acme", "guide", new EmailCaptureRequestDto { Email = email, Website = "" }, default);
        Assert.Equal([email], captures.Emails);
    }

    [Fact]
    public async Task ASixthSignUpForOneAddress_Is429WithRetryAfter_AndCapturesNothing()
    {
        await using var limiter = Limiter();
        var captures = new RecordingCaptures();
        var email = $"ana-{Guid.NewGuid():N}@example.test";

        for (var i = 0; i < 5; i++)
            await Controller(limiter, captures).Capture("acme", "guide", new EmailCaptureRequestDto { Email = email }, default);
        var controller = Controller(limiter, captures);
        var sixth = await controller.Capture("acme", "guide", new EmailCaptureRequestDto { Email = email }, default);

        var rejected = Assert.IsType<ObjectResult>(sixth.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.StatusCode);
        Assert.Equal(RateLimitRejection.Code, Assert.IsType<ApiErrorResponse>(rejected.Value).Code);
        Assert.True(int.Parse(controller.Response.Headers.RetryAfter.ToString()) is > 0 and <= 600);
        Assert.Equal(5, captures.Emails.Count);
    }

    private static PublicLandingPagesController Controller(CaptureEmailRateLimiter limiter, RecordingCaptures captures) =>
        new(new OnePage(), new NoPageViews(), captures, new NoCheckout(), limiter,
            new HostingEnvironment { EnvironmentName = Environments.Production }, NullLogger<PublicLandingPagesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class OnePage : IPublicLandingPageService
    {
        public Task<LandingPageWithSectionsResponseDto?> GetPublishedAsync(string creatorSlug, string landingPageSlug, CancellationToken ct)
            => Task.FromResult<LandingPageWithSectionsResponseDto?>(new LandingPageWithSectionsResponseDto { Id = 7, CreatorId = 3, ProductId = 5 });

        public Task<PublicCreatorPagesResponseDto?> GetCreatorPagesAsync(string creatorSlug, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingCaptures : IEmailCaptureService
    {
        public List<string> Emails { get; } = [];

        public Task CaptureAsync(int landingPageId, int? productId, int creatorId, string email, CancellationToken ct)
        {
            Emails.Add(email);
            return Task.CompletedTask;
        }

        public Task<CapturesByPeriodRow> GetCaptureCountsByPeriodAsync(int landingPageId, StatsPeriods periods, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<int, int>> GetCaptureCountsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoPageViews : IPageViewService
    {
        public Task RecordAsync(int landingPageId, Guid visitorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<LandingPageAnalyticsResponseDto> GetLandingPageStatsAsync(int landingPageId, StatsPeriods periods, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<LandingPageViewsSummaryDto>> GetViewsSummaryByCreatorAsync(string creatorSlug, int ownerUserId, CancellationToken ct) => throw new NotSupportedException();
        public Task InvalidateViewsSummaryAsync(string creatorSlug, int ownerUserId) => throw new NotSupportedException();
    }

    private sealed class NoCheckout : IOrderCheckoutService
    {
        public Task<CreateCheckoutResultDto> CreateCheckoutAsync(string creatorSlug, string landingPageSlug, string? buyerEmail, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
