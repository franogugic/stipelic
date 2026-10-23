namespace CreatorPlatform.Email.Application.Exceptions;

/// <summary>
/// The provider could not take the message right now (unreachable, or a temporary 4xx refusal): "try later", not "this
/// attempt failed". The outbox reschedules the message after <see cref="RetryAfter"/> without spending one of its
/// limited retries — the same treatment as provider throttling.
/// </summary>
public sealed class TransientEmailSendException : Exception
{
    public TransientEmailSendException(string message, TimeSpan retryAfter, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
