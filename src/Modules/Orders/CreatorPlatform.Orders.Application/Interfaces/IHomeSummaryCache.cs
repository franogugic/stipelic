using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IHomeSummaryCache
{
    bool TryGet(string creatorSlug, int ownerUserId, out HomeSummaryDto? value);
    void Set(string creatorSlug, int ownerUserId, HomeSummaryDto value);
}
