namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorPlanResponseDto
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public int PriceCents { get; init; }

    public string BillingInterval { get; init; } = string.Empty;

    public int PlatformFeeBasisPoints { get; init; }

    public IReadOnlyDictionary<string, int> Limits { get; init; } = new Dictionary<string, int>();
}
