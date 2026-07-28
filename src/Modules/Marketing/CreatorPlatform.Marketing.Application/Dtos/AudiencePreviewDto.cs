namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record AudiencePreviewDto(
    int RecipientCount,
    int MonthlyLimit,
    int UsedThisMonth,
    int Remaining);
