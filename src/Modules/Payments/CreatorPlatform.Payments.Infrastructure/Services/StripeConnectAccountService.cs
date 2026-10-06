using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure.Services;

/// <summary>
/// Creates Stripe Connect (Express-dashboard) accounts using the v1 Accounts API with explicit
/// `controller` properties — NOT the legacy `type: "express"` shorthand. This keeps the platform in
/// full control of who pays fees/absorbs losses (both "application") while the connected account still
/// gets Stripe's hosted Express dashboard.
///
/// Decision: Stripe.net 51.2.0 also exposes a v2 Accounts API (`StripeClient.V2.Core.Accounts`), but its
/// account shape is `Configuration.Merchant` / `Configuration.Recipient` — a different model with no
/// `Fees.Payer` / `Losses.Payments` concept. Task 3's checkout branch commits to the classic Connect
/// destination-charge mechanism (`PaymentIntentData.ApplicationFeeAmount` + `TransferData.Destination`),
/// which requires an account created through this v1 Controller-based flow. Mixing v2 account creation
/// with v1 charge-time Connect semantics would be an unsupported cross-generation combination, so v1 is
/// used end-to-end for Connect in this codebase.
/// </summary>
public sealed class StripeConnectAccountService : IConnectAccountService
{
    private readonly StripeClient _stripeClient;
    private readonly StripeOptions _options;
    private readonly ILogger<StripeConnectAccountService> _logger;

    public StripeConnectAccountService(
        StripeClient stripeClient,
        IOptions<StripeOptions> options,
        ILogger<StripeConnectAccountService> logger)
    {
        _stripeClient = stripeClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateAccountAsync(string countryCode, string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var createOptions = new AccountCreateOptions
        {
            Country = countryCode,
            Email = email,
            Controller = new AccountControllerOptions
            {
                Fees = new AccountControllerFeesOptions { Payer = "application" },
                Losses = new AccountControllerLossesOptions { Payments = "application" },
                StripeDashboard = new AccountControllerStripeDashboardOptions { Type = "express" },
                RequirementCollection = "stripe",
            },
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
        };

        var service = new AccountService(_stripeClient);
        var account = await service.CreateAsync(createOptions, requestOptions: null, ct);

        return account.Id;
    }

    public async Task<string> CreateOnboardingLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var linkOptions = new AccountLinkCreateOptions
        {
            Account = accountId,
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl,
            Type = "account_onboarding",
        };

        var service = new AccountLinkService(_stripeClient);
        var link = await service.CreateAsync(linkOptions, requestOptions: null, ct);

        return link.Url;
    }

    public async Task<string> CreateDashboardLoginLinkAsync(string accountId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var link = await new AccountLoginLinkService(_stripeClient).CreateAsync(accountId, options: null, requestOptions: null, ct);
        return link.Url;
    }

    public async Task<PayoutScheduleDto?> GetPayoutScheduleAsync(string accountId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogWarning("Stripe secret key is not configured; payout schedule unavailable.");
            return null;
        }

        try
        {
            var account = await new AccountService(_stripeClient).GetAsync(accountId, options: null, requestOptions: null, ct);
            var schedule = account.Settings?.Payouts?.Schedule;
            if (schedule is null || string.IsNullOrEmpty(schedule.Interval))
                return null;

            return new PayoutScheduleDto(
                schedule.Interval,
                (int)schedule.DelayDays,
                schedule.Interval == "weekly" ? schedule.WeeklyAnchor : null,
                schedule.Interval == "monthly" ? (int)schedule.MonthlyAnchor : null);
        }
        catch (Exception exception) when (exception is StripeException or HttpRequestException
                                              || (exception is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Stripe errors, network failures and client timeouts — but not the caller cancelling the request.
            _logger.LogWarning(exception, "Could not read the payout schedule from Stripe. AccountId: {AccountId}", accountId);
            return null;
        }
    }
}
