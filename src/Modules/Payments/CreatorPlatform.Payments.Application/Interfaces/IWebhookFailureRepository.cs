using CreatorPlatform.Payments.Domain;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IWebhookFailureRepository
{
    Task AddAsync(WebhookFailure failure, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
