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
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            Status = plan.Status.ToString(),
            Currency = ToCurrencyCode(plan.Currency),
            PriceCents = plan.PriceCents,
            BillingInterval = plan.BillingInterval.ToString(),
            PlatformFeeBasisPoints = plan.PlatformFeeBasisPoints,
            Limits = plan.Limits.ToDictionary(limit => limit.LimitKey, limit => limit.LimitValue)
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
