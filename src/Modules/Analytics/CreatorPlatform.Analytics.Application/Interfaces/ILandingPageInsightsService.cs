using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface ILandingPageInsightsService
{
    Task<TimeSeriesResponseDto> GetTimeSeriesAsync(
        int landingPageId,
        DateTimeOffset landingPageCreatedAt,
        TimeSeriesPeriod period,
        CancellationToken ct);
}
