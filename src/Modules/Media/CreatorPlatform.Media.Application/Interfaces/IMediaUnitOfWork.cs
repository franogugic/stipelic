namespace CreatorPlatform.Media.Application.Interfaces;

public interface IMediaUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
