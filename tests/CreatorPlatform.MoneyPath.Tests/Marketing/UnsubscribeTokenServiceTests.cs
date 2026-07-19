using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class UnsubscribeTokenServiceTests
{
    private static UnsubscribeTokenService BuildService(string secret = "test-secret-value-1234567890")
        => new(Options.Create(new MarketingOptions { UnsubscribeTokenSecret = secret }));

    [Fact]
    public void Create_ThenTryParse_RoundTrips()
    {
        var service = BuildService();

        var token = service.Create(42, "someone@example.com");
        var payload = service.TryParse(token);

        Assert.NotNull(payload);
        Assert.Equal(42, payload!.CreatorId);
        Assert.Equal("someone@example.com", payload.Email);
    }

    [Fact]
    public void Create_NormalizesEmailCaseAndWhitespace()
    {
        var service = BuildService();

        var token = service.Create(1, "  Someone@Example.COM  ");
        var payload = service.TryParse(token);

        Assert.NotNull(payload);
        Assert.Equal("someone@example.com", payload!.Email);
    }

    [Fact]
    public void TryParse_DifferentEmailCasingProducesSameNormalizedResult()
    {
        var service = BuildService();

        var tokenLower = service.Create(7, "test@example.com");
        var tokenUpper = service.Create(7, "TEST@EXAMPLE.COM");

        var payloadLower = service.TryParse(tokenLower);
        var payloadUpper = service.TryParse(tokenUpper);

        Assert.Equal(payloadLower!.Email, payloadUpper!.Email);
        Assert.Equal(payloadLower.CreatorId, payloadUpper.CreatorId);
    }

    [Fact]
    public void TryParse_TamperedSignature_ReturnsNull()
    {
        var service = BuildService();
        var token = service.Create(1, "someone@example.com");
        var parts = token.Split('.');

        var tamperedSignature = parts[1] == "aaaa" ? "bbbb" : "aaaa";
        var tamperedToken = $"{parts[0]}.{tamperedSignature}";

        Assert.Null(service.TryParse(tamperedToken));
    }

    [Fact]
    public void TryParse_TamperedPayload_ReturnsNull()
    {
        var serviceA = BuildService();
        var tokenForCreatorOne = serviceA.Create(1, "someone@example.com");
        var tokenForCreatorTwo = serviceA.Create(2, "someone@example.com");

        var parts1 = tokenForCreatorOne.Split('.');
        var parts2 = tokenForCreatorTwo.Split('.');

        // Splice creator-2's payload with creator-1's signature — signature no longer matches.
        var splicedToken = $"{parts2[0]}.{parts1[1]}";

        Assert.Null(serviceA.TryParse(splicedToken));
    }

    [Fact]
    public void TryParse_SignedWithDifferentSecret_ReturnsNull()
    {
        var serviceA = BuildService("secret-a-0000000000000000000000");
        var serviceB = BuildService("secret-b-0000000000000000000000");

        var token = serviceA.Create(1, "someone@example.com");

        Assert.Null(serviceB.TryParse(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("only.one.dot.too.many")]
    [InlineData("!!!.###")]
    public void TryParse_MalformedToken_ReturnsNull(string malformedToken)
    {
        var service = BuildService();

        Assert.Null(service.TryParse(malformedToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_BlankSecret_ThrowsInvalidOperationException(string? blankSecret)
    {
        Assert.Throws<InvalidOperationException>(
            () => new UnsubscribeTokenService(Options.Create(new MarketingOptions { UnsubscribeTokenSecret = blankSecret! })));
    }
}
