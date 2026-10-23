using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Api.Webhooks;

/// <param name="Error">The latest error's first line, at most 500 characters.</param>
/// <param name="Replayable">A handler failure (its payload is the event); the others are fixed by hand and resolved.</param>
public sealed record WebhookFailureDto(
    int Id,
    string Provider,
    string EventId,
    string EventType,
    string Kind,
    string Error,
    int Attempts,
    DateTimeOffset OccurredAt,
    DateTimeOffset LastFailedAt,
    bool IsResolved,
    DateTimeOffset? ResolvedAt,
    bool Replayable);

/// <summary>The platform admin's view of failed webhooks: list them, replay a handler failure, resolve one by hand.</summary>
public sealed class WebhookFailureAdminService
{
    public const int MaxLimit = 200;
    private const int ErrorPreviewLength = 500;

    private readonly IWebhookFailureRepository _failures;
    private readonly IStripeWebhookService _stripeWebhookService;
    private readonly StripeWebhookProcessor _processor;

    public WebhookFailureAdminService(
        IWebhookFailureRepository failures,
        IStripeWebhookService stripeWebhookService,
        StripeWebhookProcessor processor)
    {
        _failures = failures;
        _stripeWebhookService = stripeWebhookService;
        _processor = processor;
    }

    /// <param name="status">"open" (default), "resolved" or "all".</param>
    public async Task<List<WebhookFailureDto>> ListAsync(string? status, int limit, CancellationToken ct)
    {
        bool? resolved = (status ?? "open").Trim().ToLowerInvariant() switch
        {
            "open" => false,
            "resolved" => true,
            "all" => null,
            _ => throw new BadRequestException("Status must be open, resolved or all.")
        };

        var rows = await _failures.ListAsync(resolved, Math.Clamp(limit <= 0 ? 50 : limit, 1, MaxLimit), ct);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>Runs the stored event through its handler again. Success resolves it (and any other open row of the
    /// event); a failure counts another attempt and answers 409 with the error.</summary>
    public async Task<WebhookFailureDto> ReplayAsync(int id, CancellationToken ct)
    {
        var failure = await GetOpenAsync(id, ct);
        if (failure.Kind != WebhookFailureKind.HandlerFailed)
            throw new ConflictException("This failure needs a manual fix and cannot be replayed; resolve it once fixed.");

        var webhookEvent = _stripeWebhookService.Parse(failure.Payload);
        var outcome = await _processor.ProcessAsync(webhookEvent, failure.Payload, ct);
        if (outcome != StripeWebhookOutcome.Processed)
            throw new ConflictException($"The replay failed again ({outcome}). See the failure's latest error.");

        failure.MarkResolved(DateTimeOffset.UtcNow);
        await _failures.SaveChangesAsync(ct);
        return ToDto(failure);
    }

    public async Task<WebhookFailureDto> ResolveAsync(int id, CancellationToken ct)
    {
        var failure = await GetOpenAsync(id, ct);
        failure.MarkResolved(DateTimeOffset.UtcNow);
        await _failures.SaveChangesAsync(ct);
        return ToDto(failure);
    }

    private async Task<WebhookFailure> GetOpenAsync(int id, CancellationToken ct)
    {
        var failure = await _failures.GetByIdAsync(id, ct) ?? throw new NotFoundException("Webhook failure not found.");
        if (failure.IsResolved)
            throw new ConflictException("This webhook failure is already resolved.");
        return failure;
    }

    private static WebhookFailureDto ToDto(WebhookFailure failure)
    {
        var firstLine = failure.ErrorMessage.Split('\n', 2)[0].TrimEnd('\r');
        var error = firstLine.Length > ErrorPreviewLength ? firstLine[..ErrorPreviewLength] : firstLine;

        return new WebhookFailureDto(
            failure.Id, failure.Provider, failure.EventId, failure.EventType, failure.Kind.ToString(), error, failure.Attempts,
            failure.OccurredAt, failure.LastFailedAt, failure.IsResolved, failure.ResolvedAt,
            Replayable: failure.Kind == WebhookFailureKind.HandlerFailed);
    }
}
