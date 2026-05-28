namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorPlanResponseDto
{
    public Guid PublicId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public int MonthlyPriceCents { get; init; }

    public int YearlyPriceCents { get; init; }

    public int MaxContacts { get; init; }

    public int MaxLandingPages { get; init; }

    public int MaxProducts { get; init; }

    public int MaxTeamMembers { get; init; }

    public int MaxEmailSendsPerMonth { get; init; }

    public int PlatformFeeBasisPoints { get; init; }

    public string FeaturesJson { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
