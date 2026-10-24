using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <inheritdoc cref="ILeadMagnetDelivery"/>
public sealed class LeadMagnetDelivery : ILeadMagnetDelivery
{
    /// <summary>A repeat sign-up re-sends the link at most this often (owner, 2026-10-23).</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromHours(24);

    private readonly CreatorPlatformDbContext _context;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly MarketingOptions _options;

    public LeadMagnetDelivery(
        CreatorPlatformDbContext context,
        IEmailOutboxService emailOutboxService,
        IUnsubscribeTokenService unsubscribeTokenService,
        IOptions<MarketingOptions> options)
    {
        _context = context;
        _emailOutboxService = emailOutboxService;
        _unsubscribeTokenService = unsubscribeTokenService;
        _options = options.Value;
    }

    public async Task DeliverAsync(int creatorId, int landingPageId, string normalizedEmail, DateTimeOffset now, CancellationToken ct)
    {
        var delivery = await (
            from lp in _context.Set<LandingPage>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on lp.ProductId equals p.Id
            join ec in _context.Set<EmailCapture>().AsNoTracking() on lp.Id equals ec.LandingPageId
            join c in _context.Set<Creator>().AsNoTracking() on lp.CreatorId equals c.Id
            from s in _context.Set<CreatorSettings>().AsNoTracking().Where(s => s.CreatorId == c.Id).DefaultIfEmpty()
            where lp.Id == landingPageId
                && lp.CreatorId == creatorId
                && lp.Type == LandingPageType.LeadGen
                && p.FileBlobPath != null
                && ec.Email == normalizedEmail
            select new
            {
                EmailCaptureId = ec.Id,
                ec.CapturedAt,
                ProductId = p.Id,
                ProductName = p.Name,
                p.FileContentType,
                p.FileSizeBytes,
                p.ThumbnailUrl,
                CreatorName = c.Name,
                BrandName = s != null ? s.BrandName : null,
                BrandColor = s != null ? s.PrimaryColor : null,
                LogoUrl = s != null ? s.LogoUrl : null,
                SupportEmail = s != null ? s.SupportEmail : null,
                Unsubscribed = _context.Set<Unsubscribe>().Any(u => u.CreatorId == creatorId && u.Email == normalizedEmail),
            }).FirstOrDefaultAsync(ct);

        // Not a lead page, no document, or the capture isn't there: nothing to send.
        if (delivery is null)
            return;

        // One link per sign-up and document. A repeat sign-up (or a parallel duplicate) finds the existing one.
        var newToken = AccessGrant.NewToken();
        await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO products.access_grants
                ("Token", "CreatorId", "ProductId", "EmailCaptureId", "Email", "CreatedAt", "DownloadCount")
            VALUES ({newToken}, {creatorId}, {delivery.ProductId}, {delivery.EmailCaptureId}, {normalizedEmail}, {now}, 0)
            ON CONFLICT ("EmailCaptureId", "ProductId") WHERE "EmailCaptureId" IS NOT NULL DO NOTHING
            """, ct);

        // Claim the send: the row lock makes parallel sign-ups agree on one email, and the time check limits re-sends.
        var resendBefore = now - ResendInterval;
        var token = (await _context.Database.SqlQuery<string>($"""
            UPDATE products.access_grants
            SET "LastSentAt" = {now}
            WHERE "EmailCaptureId" = {delivery.EmailCaptureId}
              AND "ProductId" = {delivery.ProductId}
              AND "RevokedAt" IS NULL
              AND ("LastSentAt" IS NULL OR "LastSentAt" <= {resendBefore})
            RETURNING "Token" AS "Value"
            """).ToListAsync(ct)).SingleOrDefault();

        if (token is null)
            return;

        var brandName = string.IsNullOrWhiteSpace(delivery.BrandName) ? delivery.CreatorName : delivery.BrandName;
        await _emailOutboxService.QueueLeadMagnetAsync(
            creatorId,
            normalizedEmail,
            $"lead:{delivery.EmailCaptureId}:{delivery.ProductId}",
            new LeadMagnetEmail(
                normalizedEmail,
                delivery.ProductName,
                EmailFileLabel.From(delivery.FileContentType, delivery.FileSizeBytes),
                delivery.ThumbnailUrl,
                delivery.CapturedAt,
                $"{_options.FrontendBaseUrl.TrimEnd('/')}/d/{token}",
                brandName,
                delivery.BrandColor,
                delivery.LogoUrl,
                delivery.SupportEmail,
                delivery.Unsubscribed ? null : _unsubscribeTokenService.BuildUnsubscribePageUrl(creatorId, normalizedEmail)),
            ct);

        // The outbox row joins the sign-up's transaction.
        await _context.SaveChangesAsync(ct);
    }
}
