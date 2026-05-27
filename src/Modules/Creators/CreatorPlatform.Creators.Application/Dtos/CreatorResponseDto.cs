namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorResponseDto
{
    public Guid PublicId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DefaultCurrency { get; init; } = string.Empty;

    public string PlanCode { get; init; } = string.Empty;
}
