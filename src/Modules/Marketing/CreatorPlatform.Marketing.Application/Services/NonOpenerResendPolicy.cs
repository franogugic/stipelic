using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>Why a non-opener resend is not possible right now — the codes the API returns (and the 409 codes of
/// a refused send).</summary>
public static class NonOpenerResendReasons
{
    public const string NotSent = "NOT_SENT";
    public const string IsResend = "IS_RESEND";
    public const string LimitReached = "LIMIT_REACHED";
    public const string ResendPending = "RESEND_PENDING";
    public const string TooLate = "TOO_LATE";
    public const string TooEarly = "TOO_EARLY";
    public const string NoRecipients = "NO_RECIPIENTS";

    public static string Message(string reason) => reason switch
    {
        NotSent => "Only a sent campaign can be resent to the people who did not open it.",
        IsResend => "A resend cannot be resent; resend the original campaign instead.",
        LimitReached => $"This campaign has already been resent {Campaign.MaxNonOpenerResends} times.",
        ResendPending => "A resend of this campaign is already scheduled.",
        TooLate => $"Resends are possible for {NonOpenerResendPolicy.Window.TotalDays:0} days after the original was sent.",
        TooEarly => $"A resend is possible {NonOpenerResendPolicy.MinGap.TotalHours:0} hours after the last send.",
        NoRecipients => "Everyone who received this campaign has opened it, unsubscribed or did not receive it.",
        _ => "This campaign cannot be resent right now."
    };
}

/// <summary>The outcome of <see cref="NonOpenerResendPolicy.Evaluate"/>. <see cref="Used"/> is the number of
/// resends that hold a slot (sent or scheduled). The dates are null when the original was never sent or is a
/// resend itself.</summary>
public sealed record NonOpenerResendDecision(
    bool Eligible,
    string? Reason,
    DateTimeOffset? EligibleFrom,
    DateTimeOffset? EligibleUntil,
    int Used);

/// <summary>
/// When a sent campaign may be resent to its non-openers (owner, 2026-10-22): up to
/// <see cref="Campaign.MaxNonOpenerResends"/> resends, each at least <see cref="MinGap"/> after the original and
/// after the previous resend went out, all within <see cref="Window"/> of the original, one scheduled at a time.
/// A cancelled resend frees its slot, and so does a failed one (a scheduled resend whose dispatch failed sent
/// nothing). Pure: the caller loads the original, its resends and the next recipient count.
/// </summary>
public static class NonOpenerResendPolicy
{
    public static readonly TimeSpan MinGap = TimeSpan.FromHours(24);
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    /// <summary>The source's own problem, before anything about its resends is known: not sent, or a resend.</summary>
    public static string? SourceReason(Campaign source)
    {
        if (source.IsNonOpenerResend)
            return NonOpenerResendReasons.IsResend;

        return source.Status == CampaignStatus.Queued && source.QueuedAt is not null
            ? null
            : NonOpenerResendReasons.NotSent;
    }

    /// <summary>Resends that hold one of the slots: sent or still scheduled.</summary>
    public static bool HoldsSlot(Campaign resend) =>
        resend.Status is CampaignStatus.Queued or CampaignStatus.Scheduled;

    public static NonOpenerResendDecision Evaluate(
        Campaign original, IReadOnlyCollection<Campaign> resends, int nextRecipientCount, DateTimeOffset now)
    {
        if (SourceReason(original) is { } sourceReason)
            return new NonOpenerResendDecision(false, sourceReason, null, null, 0);

        var used = resends.Count(HoldsSlot);
        var lastSentAt = resends
            .Where(r => r.Status == CampaignStatus.Queued && r.QueuedAt is not null)
            .Select(r => r.QueuedAt!.Value)
            .Append(original.QueuedAt!.Value)
            .Max();
        var eligibleFrom = lastSentAt + MinGap;
        var eligibleUntil = original.QueuedAt!.Value + Window;

        string? reason =
            used >= Campaign.MaxNonOpenerResends ? NonOpenerResendReasons.LimitReached
            : resends.Any(r => r.Status == CampaignStatus.Scheduled) ? NonOpenerResendReasons.ResendPending
            // The next possible time already falls after the window: closed now, not "too early".
            : now > eligibleUntil || eligibleFrom > eligibleUntil ? NonOpenerResendReasons.TooLate
            : now < eligibleFrom ? NonOpenerResendReasons.TooEarly
            : nextRecipientCount <= 0 ? NonOpenerResendReasons.NoRecipients
            : null;

        return new NonOpenerResendDecision(reason is null, reason, eligibleFrom, eligibleUntil, used);
    }
}
