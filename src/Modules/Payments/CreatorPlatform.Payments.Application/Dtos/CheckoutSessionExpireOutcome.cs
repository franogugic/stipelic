namespace CreatorPlatform.Payments.Application.Dtos;

/// <summary>What happened when expiring a Checkout session. Transport/provider failures are not an outcome —
/// they throw, because then the session's state is unknown.</summary>
public enum CheckoutSessionExpireOutcome
{
    /// <summary>The session was open and is now expired: it can no longer be paid.</summary>
    Expired,

    /// <summary>The session had already expired: it could not be paid anyway.</summary>
    AlreadyExpired,

    /// <summary>The customer completed the session (paid) before it could be expired.</summary>
    AlreadyCompleted
}
