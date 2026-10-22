using System.Threading.RateLimiting;
using CreatorPlatform.Api.Health;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>A named fixed-window rate-limit policy: at most <paramref name="PermitLimit"/> requests per
/// <paramref name="Window"/>, counted per client IP. No queueing.</summary>
public sealed record RateLimitPolicyDefinition(string Name, int PermitLimit, TimeSpan Window);

/// <summary>
/// Every rate-limit policy the API knows, in one place. A new policy is one constant plus one catalog line;
/// <c>Program.cs</c> registers whatever the catalog holds, and <c>[EnableRateLimiting]</c> refers to the constant.
/// </summary>
public static class RateLimitPolicies
{
    public const string Register = "Register";
    public const string Login = "Login";
    public const string VerifyEmail = "VerifyEmail";
    public const string ResendVerificationEmail = "ResendVerificationEmail";
    public const string RequestPasswordReset = "RequestPasswordReset";
    public const string ResetPassword = "ResetPassword";
    public const string RequestEmailChange = "RequestEmailChange";
    public const string ResendEmailChange = "ResendEmailChange";
    public const string ConfirmEmailChange = "ConfirmEmailChange";
    public const string UpdateProfile = "UpdateProfile";
    public const string InspectResetToken = "InspectResetToken";
    public const string CreateCreator = "CreateCreator";
    public const string CreateLandingPage = "CreateLandingPage";
    public const string UpdateLandingPage = "UpdateLandingPage";
    public const string CreateProduct = "CreateProduct";
    public const string UpdateProduct = "UpdateProduct";
    public const string UpdateCreatorSettings = "UpdateCreatorSettings";
    public const string StartCreatorCheckout = "StartCreatorCheckout";
    public const string ContinueFree = "ContinueFree";
    public const string DeleteWorkspace = "DeleteWorkspace";
    public const string ConnectOnboarding = "ConnectOnboarding";
    public const string ConnectDashboardLink = "ConnectDashboardLink";
    public const string AdminPayouts = "AdminPayouts";
    public const string RequestPayout = "RequestPayout";
    public const string Unsubscribe = "Unsubscribe";
    public const string OrderReceipt = "OrderReceipt";
    public const string PublicCreatorPages = "PublicCreatorPages";
    public const string SendCampaign = "SendCampaign";
    public const string ResendFailedCampaign = "ResendFailedCampaign";
    public const string CampaignInsights = "CampaignInsights";
    public const string ExportOrders = "ExportOrders";
    public const string ExportContacts = "ExportContacts";
    public const string DeleteContact = "DeleteContact";
    public const string MutateTemplate = "MutateTemplate";
    public const string RequestMediaUpload = "RequestMediaUpload";

    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    public static IReadOnlyList<RateLimitPolicyDefinition> Catalog { get; } =
    [
        new(Register, 5, TenMinutes),
        new(Login, 20, TenMinutes),
        new(VerifyEmail, 10, TenMinutes),
        new(ResendVerificationEmail, 3, TenMinutes),
        new(RequestPasswordReset, 3, TenMinutes),
        new(ResetPassword, 10, TenMinutes),
        new(RequestEmailChange, 5, TenMinutes),
        new(ResendEmailChange, 3, TenMinutes),
        new(ConfirmEmailChange, 10, TenMinutes),
        new(UpdateProfile, 30, TenMinutes),
        new(InspectResetToken, 20, TenMinutes),
        new(CreateCreator, 3, OneHour),
        new(CreateLandingPage, 10, TenMinutes),
        new(UpdateLandingPage, 60, TenMinutes),
        new(CreateProduct, 10, TenMinutes),
        new(UpdateProduct, 60, TenMinutes),
        new(UpdateCreatorSettings, 30, TenMinutes),
        new(StartCreatorCheckout, 10, TenMinutes),
        new(ContinueFree, 5, TenMinutes),
        // Deleting the workspace calls Stripe (cancel the subscription / expire the Checkout session).
        new(DeleteWorkspace, 5, TenMinutes),
        new(ConnectOnboarding, 10, TenMinutes),
        // Same limits as the onboarding link, in its own bucket.
        new(ConnectDashboardLink, 10, TenMinutes),
        new(AdminPayouts, 30, TenMinutes),
        new(RequestPayout, 5, TenMinutes),
        new(Unsubscribe, 30, TenMinutes),
        // The purchase success page polls a few times while the payment webhook is on its way.
        new(OrderReceipt, 30, TenMinutes),
        // Public "More from {brand}" list on missing pages: one call per missing-page view.
        new(PublicCreatorPages, 60, OneMinute),
        new(SendCampaign, 5, TenMinutes),
        new(ResendFailedCampaign, 5, TenMinutes),
        new(CampaignInsights, 60, OneMinute),
        new(ExportOrders, 10, TenMinutes),
        new(ExportContacts, 10, TenMinutes),
        new(DeleteContact, 60, TenMinutes),
        new(MutateTemplate, 30, TenMinutes),
        new(RequestMediaUpload, 20, TenMinutes),
    ];

    /// <summary>The global limiter every request passes through, on top of its endpoint policy.</summary>
    public static RateLimitPolicyDefinition Global { get; } = new("Global", 300, OneMinute);

    // The open-tracking pixel is exempt: Gmail and Apple fetch images through shared proxy IPs, so a per-IP
    // limit would drop genuine opens (and a rejection answers JSON, not the GIF). Safe without a limit: a
    // forged token is rejected by the HMAC check before any database query, and a repeated valid token
    // is an idempotent UPDATE that changes nothing.
    private const string OpenPixelPath = "/api/public/o";

    /// <summary>The global limiter's partition: per client IP, or no limit for the exempt paths.</summary>
    public static RateLimitPartition<string> GlobalPartition(HttpContext httpContext)
    {
        var path = httpContext.Request.Path;

        if (path.StartsWithSegments(OpenPixelPath, StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("open-pixel");

        // Probes (container platform, uptime monitors) must never be throttled into reporting the API as down.
        if (path.StartsWithSegments(HealthEndpoint.Path, StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("health");

        return Partition(httpContext, Global);
    }

    /// <summary>A fixed-window partition of <paramref name="policy"/> for the request's client IP.</summary>
    public static RateLimitPartition<string> Partition(HttpContext httpContext, RateLimitPolicyDefinition policy) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = policy.Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
}
