using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Services;

public sealed class CreatorPlanService : ICreatorPlanService
{
    private readonly ICreatorPlanRepository _creatorPlanRepository;

    public CreatorPlanService(ICreatorPlanRepository creatorPlanRepository)
    {
        _creatorPlanRepository = creatorPlanRepository;
    }

    public async Task<List<CreatorPlanResponseDto>> ListActiveAsync(CancellationToken ct)
    {
        var plans = await _creatorPlanRepository.ListActiveAsync(ct);

        return plans.Select(ToResponse).ToList();
    }

    private static CreatorPlanResponseDto ToResponse(CreatorPlan plan)
    {
        return new CreatorPlanResponseDto
        {
            PublicId = plan.PublicId,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            Currency = ToCurrencyCode(plan.Currency),
            MonthlyPriceCents = plan.MonthlyPriceCents,
            YearlyPriceCents = plan.YearlyPriceCents,
            MaxContacts = plan.MaxContacts,
            MaxLandingPages = plan.MaxLandingPages,
            MaxProducts = plan.MaxProducts,
            MaxTeamMembers = plan.MaxTeamMembers,
            MaxEmailSendsPerMonth = plan.MaxEmailSendsPerMonth,
            PlatformFeeBasisPoints = plan.PlatformFeeBasisPoints,
            FeaturesJson = plan.FeaturesJson,
            SortOrder = plan.SortOrder
        };
    }

    private static string ToCurrencyCode(Currency currency)
    {
        return currency switch
        {
            Currency.Eur => "EUR",
            Currency.Usd => "USD",
            _ => currency.ToString()
        };
    }
}
