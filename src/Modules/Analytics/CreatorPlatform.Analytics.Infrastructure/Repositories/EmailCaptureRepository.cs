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

    public async Task<bool> AddAsync(EmailCapture capture, CancellationToken ct)
    {
        var rowsAffected = await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO analytics.email_captures ("Id", "LandingPageId", "ProductId", "Email", "CapturedAt")
             VALUES ({capture.Id}, {capture.LandingPageId}, {capture.ProductId}, {capture.Email}, {capture.CapturedAt})
             ON CONFLICT ("LandingPageId", "Email") DO NOTHING
             """,
            ct);

        return rowsAffected > 0;
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

    public async Task UpsertContactSummaryAsync(int creatorId, int landingPageId, string email, DateTimeOffset capturedAt, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO marketing.contact_summaries
                 ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
             VALUES ({creatorId}, {email}, {capturedAt}, {capturedAt}, ARRAY[{landingPageId}])
             ON CONFLICT ("CreatorId", "Email") DO UPDATE SET
                 "LastCapturedAt" = {capturedAt},
                 "SourceLandingPageIds" = CASE
                     WHEN {landingPageId} = ANY(contact_summaries."SourceLandingPageIds")
                         THEN contact_summaries."SourceLandingPageIds"
                     ELSE array_append(contact_summaries."SourceLandingPageIds", {landingPageId})
                 END
             """,
            ct);
    }

    public async Task<List<CapturesBucketRow>> GetBucketedCapturesAsync(
        int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
    {
        // bucketUnit comes from a fixed server-side map (never from raw query string), so it is safe to
        // interpolate into date_trunc / generate_series. Zero-filled buckets via LEFT JOIN on generate_series.
        return await _context.Database.SqlQuery<CapturesBucketRow>($"""
            WITH buckets AS (
                SELECT generate_series(
                    date_trunc({bucketUnit}, {cutoff}::timestamptz),
                    date_trunc({bucketUnit}, now()),
                    ('1 ' || {bucketUnit})::interval
                ) AS bucket_start
            )
            SELECT
                b.bucket_start                                      AS "BucketStart",
                COALESCE(COUNT(ec."Id"), 0)                         AS "CaptureCount"
            FROM buckets b
            LEFT JOIN analytics.email_captures ec
                ON ec."LandingPageId" = {landingPageId}
                AND date_trunc({bucketUnit}, ec."CapturedAt") = b.bucket_start
            GROUP BY b.bucket_start
            ORDER BY b.bucket_start
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
