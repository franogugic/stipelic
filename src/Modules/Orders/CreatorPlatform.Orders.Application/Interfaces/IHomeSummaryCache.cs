using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IHomeSummaryCache
{
    bool TryGet(string creatorSlug, out HomeSummaryDto? value);
    void Set(string creatorSlug, HomeSummaryDto value);
    void Remove(string creatorSlug);
}
