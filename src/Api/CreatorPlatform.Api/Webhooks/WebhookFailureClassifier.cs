using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;
using Stripe;

namespace CreatorPlatform.Api.Webhooks;

/// <summary>
/// Tells a failure worth Stripe's retries (an outage or a race that a later attempt gets past) from one that fails the
/// same way every time. Transient failures answer 500, so Stripe redelivers the event for up to three days — safe, the
/// handlers are idempotent. Anything not recognised as transient is permanent (a bug, bad data): retrying it would only
/// fail again, so it answers 200 and waits in the webhook failures list for a fix and a replay.
/// </summary>
public static class WebhookFailureClassifier
{
    // serialization_failure, deadlock_detected, lock_not_available, too_many_connections, admin_shutdown,
    // cannot_connect_now, unique_violation (two deliveries racing on the same row; the retry finds it done).
    private static readonly HashSet<string> TransientSqlStates =
        ["40001", "40P01", "55P03", "53300", "57P01", "57P03", "23505"];

    public static bool IsTransient(Exception exception) => exception switch
    {
        OperationCanceledException or TimeoutException or HttpRequestException => true,
        PostgresException postgres => postgres.IsTransient || TransientSqlStates.Contains(postgres.SqlState),
        NpgsqlException npgsql => npgsql.IsTransient || npgsql.InnerException is not null && IsTransient(npgsql.InnerException),
        DbUpdateConcurrencyException => true,
        RedisConnectionException or RedisTimeoutException => true,
        StripeException stripe => stripe.HttpStatusCode is 0
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.Conflict
            or >= HttpStatusCode.InternalServerError,
        _ => exception.InnerException is not null && IsTransient(exception.InnerException)
    };
}
