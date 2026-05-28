namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorPlan
{
    private readonly List<CreatorPlanLimit> _limits = [];

    private CreatorPlan()
    {
    }

    private CreatorPlan(
        string code,
        string name,
        string? description,
        CreatorPlanStatus status,
        int priceCents,
        Currency currency,
        BillingInterval billingInterval,
        int platformFeeBasisPoints,
        DateTimeOffset createdAt)
    {
        Code = code;
        Name = name;
        Description = description;
        Status = status;
        PriceCents = priceCents;
        Currency = currency;
        BillingInterval = billingInterval;
        PlatformFeeBasisPoints = platformFeeBasisPoints;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorPlan Create(
        string code,
        string name,
        string? description,
        int priceCents,
        Currency currency,
        BillingInterval billingInterval,
        int platformFeeBasisPoints,
        DateTimeOffset createdAt)
    {
        return new CreatorPlan(
            code,
            name,
            description,
            CreatorPlanStatus.Active,
            priceCents,
            currency,
            billingInterval,
            platformFeeBasisPoints,
            createdAt);
    }

    public void UpdateCommercialTerms(
        int priceCents,
        Currency currency,
        BillingInterval billingInterval,
        int platformFeeBasisPoints,
        DateTimeOffset updatedAt)
    {
        PriceCents = priceCents;
        Currency = currency;
        BillingInterval = billingInterval;
        PlatformFeeBasisPoints = platformFeeBasisPoints;
        UpdatedAt = updatedAt;
    }

    public void UpdateDetails(string name, string? description, DateTimeOffset updatedAt)
    {
        Name = name;
        Description = description;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset updatedAt)
    {
        Status = CreatorPlanStatus.Archived;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        Status = CreatorPlanStatus.Active;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CreatorPlanStatus Status { get; private set; }

    public int PriceCents { get; private set; }

    public Currency Currency { get; private set; }

    public BillingInterval BillingInterval { get; private set; }

    public int PlatformFeeBasisPoints { get; private set; }

    public IReadOnlyCollection<CreatorPlanLimit> Limits => _limits.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
