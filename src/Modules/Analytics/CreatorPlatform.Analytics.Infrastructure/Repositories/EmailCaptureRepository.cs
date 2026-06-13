using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Repositories;

public sealed class EmailCaptureRepository : IEmailCaptureRepository
{
    private readonly CreatorPlatformDbContext _context;

    public EmailCaptureRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailCapture capture, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO analytics.email_captures ("Id", "LandingPageId", "ProductId", "Email", "CapturedAt")
             VALUES ({capture.Id}, {capture.LandingPageId}, {capture.ProductId}, {capture.Email}, {capture.CapturedAt})
             ON CONFLICT ("LandingPageId", "Email") DO NOTHING
             """,
            ct);
    }

    public async Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct)
    {
        return await _context.Set<EmailCapture>()
            .AsNoTracking()
            .LongCountAsync(ec => ec.LandingPageId == landingPageId, ct);
    }

    public async Task<List<EmailCapture>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        return await _context.Set<EmailCapture>()
            .AsNoTracking()
            .Where(ec => ec.LandingPageId == landingPageId)
            .OrderByDescending(ec => ec.CapturedAt)
            .ToListAsync(ct);
    }
}
