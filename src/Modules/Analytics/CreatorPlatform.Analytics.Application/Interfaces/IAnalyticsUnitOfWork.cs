namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IAnalyticsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
