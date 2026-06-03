namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPagesUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
