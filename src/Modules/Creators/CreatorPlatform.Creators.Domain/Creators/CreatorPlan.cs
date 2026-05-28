namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorPlan
{
    private CreatorPlan()
    {
    }

    private CreatorPlan(
        Guid publicId,
        string code,
        string name,
        string description,
        Currency currency,
        int monthlyPriceCents,
        int yearlyPriceCents,
        int maxContacts,
        int maxLandingPages,
        int maxProducts,
        int maxTeamMembers,
        int maxEmailSendsPerMonth,
        int platformFeeBasisPoints,
        string featuresJson,
        bool isActive,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        Code = code;
        Name = name;
        Description = description;
        Currency = currency;
        MonthlyPriceCents = monthlyPriceCents;
        YearlyPriceCents = yearlyPriceCents;
        MaxContacts = maxContacts;
        MaxLandingPages = maxLandingPages;
        MaxProducts = maxProducts;
        MaxTeamMembers = maxTeamMembers;
        MaxEmailSendsPerMonth = maxEmailSendsPerMonth;
        PlatformFeeBasisPoints = platformFeeBasisPoints;
        FeaturesJson = featuresJson;
        IsActive = isActive;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorPlan Create(
        string code,
        string name,
        string description,
        Currency currency,
        int monthlyPriceCents,
        int yearlyPriceCents,
        int maxContacts,
        int maxLandingPages,
        int maxProducts,
        int maxTeamMembers,
        int maxEmailSendsPerMonth,
        int platformFeeBasisPoints,
        string featuresJson,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        return new CreatorPlan(
            Guid.NewGuid(),
            code,
            name,
            description,
            currency,
            monthlyPriceCents,
            yearlyPriceCents,
            maxContacts,
            maxLandingPages,
            maxProducts,
            maxTeamMembers,
            maxEmailSendsPerMonth,
            platformFeeBasisPoints,
            featuresJson,
            true,
            sortOrder,
            createdAt);
    }

    public void UpdatePricing(int monthlyPriceCents, int yearlyPriceCents, DateTimeOffset updatedAt)
    {
        MonthlyPriceCents = monthlyPriceCents;
        YearlyPriceCents = yearlyPriceCents;
        UpdatedAt = updatedAt;
    }

    public void UpdateDetails(
        string name,
        string description,
        int maxContacts,
        int maxLandingPages,
        int maxProducts,
        int maxTeamMembers,
        int maxEmailSendsPerMonth,
        int platformFeeBasisPoints,
        string featuresJson,
        int sortOrder,
        DateTimeOffset updatedAt)
    {
        Name = name;
        Description = description;
        MaxContacts = maxContacts;
        MaxLandingPages = maxLandingPages;
        MaxProducts = maxProducts;
        MaxTeamMembers = maxTeamMembers;
        MaxEmailSendsPerMonth = maxEmailSendsPerMonth;
        PlatformFeeBasisPoints = platformFeeBasisPoints;
        FeaturesJson = featuresJson;
        SortOrder = sortOrder;
        UpdatedAt = updatedAt;
    }

    public void Deactivate(DateTimeOffset updatedAt)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        IsActive = true;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }

    public int MonthlyPriceCents { get; private set; }

    public int YearlyPriceCents { get; private set; }

    public int MaxContacts { get; private set; }

    public int MaxLandingPages { get; private set; }

    public int MaxProducts { get; private set; }

    public int MaxTeamMembers { get; private set; }

    public int MaxEmailSendsPerMonth { get; private set; }

    public int PlatformFeeBasisPoints { get; private set; }

    public string FeaturesJson { get; private set; } = "[]";

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
