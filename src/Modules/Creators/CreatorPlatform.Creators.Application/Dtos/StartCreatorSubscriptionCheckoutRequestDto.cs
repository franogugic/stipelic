namespace CreatorPlatform.Creators.Application.Dtos;

/// <summary>Optional body: the plan to upgrade to, for a workspace on the Free plan. A workspace waiting for its
/// first payment sends no body (its plan was chosen at sign-up).</summary>
public sealed record StartCreatorSubscriptionCheckoutRequestDto
{
    public string? PlanCode { get; init; }
}
