using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>Tracked-link tokens: they round-trip, and anything tampered, foreign or malformed is rejected.</summary>
public class ClickTrackingTokenServiceTests
{
    private static ClickTrackingTokenService Service(string secret = "click-secret-value-1234567890") =>
        new(Options.Create(new MarketingOptions { ClickTrackingSecret = secret, ApiBaseUrl = "https://api.luma.test/" }));

    [Fact]
    public void ATokenRoundTrips_AndTheTrackedUrlUsesTheApi()
    {
        var service = Service();

        var token = service.Create(4321, 17);

        Assert.Equal(new ClickTokenPayload(4321, 17), service.TryParse(token));
        Assert.Equal($"https://api.luma.test/api/public/c/{token}", service.BuildTrackedUrl(4321, 17));
    }

    [Fact]
    public void ATamperedPayloadOrSignature_IsRejected()
    {
        var service = Service();
        var token = service.Create(4321, 17);
        var (payload, signature) = (token.Split('.')[0], token.Split('.')[1]);
        var otherPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("4321:18")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(service.TryParse($"{otherPayload}.{signature}"));
        Assert.Null(service.TryParse($"{payload}.{signature[..^2]}AA"));
        Assert.Null(service.TryParse($"{payload}.{service.Create(1, 1).Split('.')[1]}"));
    }

    [Fact]
    public void AnotherSecretsTokenOrAnOpenPixelToken_IsRejected()
    {
        var otherSecret = Service("another-secret-value-1234567890").Create(4321, 17);
        var openToken = new OpenTrackingTokenService(Options.Create(new MarketingOptions
        {
            OpenTrackingSecret = "click-secret-value-1234567890", ApiBaseUrl = "https://api.luma.test"
        })).Create(4321);

        Assert.Null(Service().TryParse(otherSecret));
        Assert.Null(Service().TryParse(openToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("NDMyMQ.AAAA")]
    public void MalformedTokens_AreRejected_WithoutThrowing(string token) =>
        Assert.Null(Service().TryParse(token));

    [Fact]
    public void AnEmptySecret_IsAConfigurationError() =>
        Assert.Throws<InvalidOperationException>(() => Service(""));
}
