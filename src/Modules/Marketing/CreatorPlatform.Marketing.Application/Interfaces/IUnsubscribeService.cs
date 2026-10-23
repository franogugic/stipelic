using CreatorPlatform.Marketing.Domain.Unsubscribes;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IUnsubscribeService
{
    /// <summary>Records the unsubscribe (idempotent). The first unsubscribe of an email that is a contact gives its
    /// <c>max_contacts</c> place back, in the same transaction; a repeat, or an email that is not a contact, changes
    /// nothing else.</summary>
    Task UnsubscribeAsync(int creatorId, string email, UnsubscribeSource source, CancellationToken ct);
}
