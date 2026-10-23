using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>The resend-to-non-openers rules on their own: reasons, their order, the slot count and the time window.</summary>
public class NonOpenerResendPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static Campaign Original(DateTimeOffset queuedAt) =>
        Campaign.CreateQueued(1, null, "Original", "Body", null, null, CampaignAudienceType.All, null, null, 10, queuedAt);

    private static Campaign SentResend(int round, DateTimeOffset queuedAt) =>
        Campaign.CreateQueued(1, null, "Again", "Body", null, null, CampaignAudienceType.CampaignNonOpeners, null, null, 5, queuedAt, 7, round);

    private static Campaign ScheduledResend(int round, DateTimeOffset scheduledAt) =>
        Campaign.CreateScheduled(1, null, "Again", "Body", null, null, CampaignAudienceType.CampaignNonOpeners, null, null, scheduledAt, Now, 7, round);

    private static Campaign CancelledResend(int round)
    {
        var resend = ScheduledResend(round, Now.AddDays(1));
        resend.Cancel(Now);
        return resend;
    }

    private static Campaign FailedResend(int round)
    {
        var resend = ScheduledResend(round, Now.AddDays(-1));
        resend.MarkFailed("No recipients.", Now);
        return resend;
    }

    private static NonOpenerResendDecision Evaluate(Campaign original, params Campaign[] resends) =>
        NonOpenerResendPolicy.Evaluate(original, resends, nextRecipientCount: 3, Now);

    [Fact]
    public void ASentOriginal_AfterADay_IsEligible_WithItsWindow()
    {
        var original = Original(Now.AddDays(-2));

        var decision = Evaluate(original);

        Assert.Equal(new NonOpenerResendDecision(true, null, Now.AddDays(-1), Now.AddDays(28), 0), decision);
    }

    [Fact]
    public void AnOriginalThatWasNeverSent_IsNotSent()
    {
        var scheduled = Campaign.CreateScheduled(1, null, "Later", "Body", null, null, CampaignAudienceType.All, null, null, Now.AddDays(1), Now);

        Assert.Equal(new NonOpenerResendDecision(false, NonOpenerResendReasons.NotSent, null, null, 0), Evaluate(scheduled));
    }

    [Fact]
    public void AResend_IsAResend() =>
        Assert.Equal(NonOpenerResendReasons.IsResend, Evaluate(SentResend(1, Now.AddDays(-3))).Reason);

    [Fact]
    public void TheDayAfterTheOriginal_OrAfterTheLastSentResend_IsTooEarly()
    {
        Assert.Equal(NonOpenerResendReasons.TooEarly, Evaluate(Original(Now.AddHours(-23))).Reason);

        var decision = Evaluate(Original(Now.AddDays(-5)), SentResend(1, Now.AddHours(-10)));
        Assert.Equal((NonOpenerResendReasons.TooEarly, Now.AddHours(14)), (decision.Reason, decision.EligibleFrom));
    }

    [Fact]
    public void After30Days_OrWhenTheNextChanceFallsAfterThem_ItIsTooLate()
    {
        Assert.Equal(NonOpenerResendReasons.TooLate, Evaluate(Original(Now.AddDays(-30).AddMinutes(-1))).Reason);
        Assert.Equal(NonOpenerResendReasons.TooLate,
            Evaluate(Original(Now.AddDays(-29.5)), SentResend(1, Now.AddHours(-1))).Reason);
    }

    [Fact]
    public void AScheduledResend_IsPending_AndHoldsASlot()
    {
        var decision = Evaluate(Original(Now.AddDays(-5)), SentResend(1, Now.AddDays(-3)), ScheduledResend(2, Now.AddHours(5)));

        Assert.Equal((NonOpenerResendReasons.ResendPending, 2), (decision.Reason, decision.Used));
    }

    [Fact]
    public void FiveResends_ReachTheLimit_EvenWhenTheFifthIsOnlyScheduled()
    {
        var original = Original(Now.AddDays(-10));
        var resends = Enumerable.Range(1, 4).Select(r => SentResend(r, Now.AddDays(-10 + r))).Append(ScheduledResend(5, Now.AddHours(3))).ToArray();

        Assert.Equal((NonOpenerResendReasons.LimitReached, 5), (Evaluate(original, resends).Reason, Evaluate(original, resends).Used));
    }

    [Fact]
    public void CancelledAndFailedResends_FreeTheirSlots()
    {
        var original = Original(Now.AddDays(-10));
        var resends = Enumerable.Range(1, 4).Select(r => SentResend(r, Now.AddDays(-10 + r)))
            .Append(CancelledResend(5)).Append(FailedResend(5)).ToArray();

        var decision = Evaluate(original, resends);

        Assert.Equal((true, 4), (decision.Eligible, decision.Used));
    }

    [Fact]
    public void WithNobodyLeft_ThereAreNoRecipients() =>
        Assert.Equal(NonOpenerResendReasons.NoRecipients,
            NonOpenerResendPolicy.Evaluate(Original(Now.AddDays(-2)), [], nextRecipientCount: 0, Now).Reason);

    [Fact]
    public void TheLimit_IsReportedBeforeTimeAndRecipients()
    {
        var original = Original(Now.AddDays(-31));
        var resends = Enumerable.Range(1, 5).Select(r => SentResend(r, Now.AddDays(-31 + r))).ToArray();

        Assert.Equal(NonOpenerResendReasons.LimitReached,
            NonOpenerResendPolicy.Evaluate(original, resends, nextRecipientCount: 0, Now).Reason);
    }
}
