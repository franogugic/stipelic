namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorPlanLimit
{
    private CreatorPlanLimit()
    {
    }

    private CreatorPlanLimit(
        CreatorPlan plan,
        string limitKey,
        int limitValue,
        DateTimeOffset createdAt)
    {
        Plan = plan;
        LimitKey = limitKey;
        LimitValue = limitValue;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorPlanLimit Create(
        CreatorPlan plan,
        string limitKey,
        int limitValue,
        DateTimeOffset createdAt)
    {
        return new CreatorPlanLimit(plan, limitKey, limitValue, createdAt);
    }

    public void UpdateValue(int limitValue, DateTimeOffset updatedAt)
    {
        LimitValue = limitValue;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public int PlanId { get; private set; }

    public CreatorPlan Plan { get; private set; } = null!;

    public string LimitKey { get; private set; } = string.Empty;

    public int LimitValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
