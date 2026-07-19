namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorUsageCounter
{
    private CreatorUsageCounter()
    {
    }

    private CreatorUsageCounter(
        Creator creator,
        string usageKey,
        int usedValue,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset createdAt)
    {
        Creator = creator;
        UsageKey = usageKey;
        UsedValue = usedValue;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorUsageCounter Create(
        Creator creator,
        string usageKey,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset createdAt)
    {
        return new CreatorUsageCounter(creator, usageKey, 0, periodStart, periodEnd, createdAt);
    }

    public void AddUsage(int amount, DateTimeOffset updatedAt)
    {
        UsedValue += amount;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public int CreatorId { get; private set; }

    public Creator Creator { get; private set; } = null!;

    public string UsageKey { get; private set; } = string.Empty;

    public int UsedValue { get; private set; }

    public DateTimeOffset PeriodStart { get; private set; }

    public DateTimeOffset PeriodEnd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
