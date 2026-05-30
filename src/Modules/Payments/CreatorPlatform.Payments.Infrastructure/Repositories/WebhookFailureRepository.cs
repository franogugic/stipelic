using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Payments.Infrastructure.Repositories;

public sealed class WebhookFailureRepository : IWebhookFailureRepository
{
    private readonly CreatorPlatformDbContext _context;

    public WebhookFailureRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WebhookFailure failure, CancellationToken ct)
    {
        await _context.Set<WebhookFailure>().AddAsync(failure, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
