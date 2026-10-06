using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface ILandingPageTimeSeriesCache
{
    bool TryGet(int landingPageId, TimeSeriesPeriod period, out TimeSeriesResponseDto? value);
    void Set(int landingPageId, TimeSeriesPeriod period, TimeSeriesResponseDto value);

    /// <summary>Drops every cached period for the landing page.</summary>
    void Remove(int landingPageId);
}
