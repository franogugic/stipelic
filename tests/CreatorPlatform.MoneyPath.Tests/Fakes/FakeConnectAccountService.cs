using CreatorPlatform.Payments.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeConnectAccountService : IConnectAccountService
{
    private readonly List<string> _callLog;

    public FakeConnectAccountService(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public string AccountIdToReturn { get; set; } = "acct_fake123";
    public string UrlToReturn { get; set; } = "https://connect.stripe.com/setup/fake";

    public int CreateAccountCallCount { get; private set; }
    public int CreateOnboardingLinkCallCount { get; private set; }

    public Task<string> CreateAccountAsync(string countryCode, string email, CancellationToken ct)
    {
        CreateAccountCallCount++;
        _callLog.Add("CreateAccount");
        return Task.FromResult(AccountIdToReturn);
    }

    public Task<string> CreateOnboardingLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct)
    {
        CreateOnboardingLinkCallCount++;
        _callLog.Add("CreateOnboardingLink");
        return Task.FromResult(UrlToReturn);
    }
}
