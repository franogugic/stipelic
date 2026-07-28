namespace CreatorPlatform.Orders.Domain.Orders;

/// <summary>
/// Pure reference implementation of the (CreatedAt, PublicId) keyset comparison used to page
/// through orders newest-first. <c>CreatedAt</c> alone isn't unique, so <c>PublicId</c> is the
/// tie-break — not the internal identity <c>Id</c>, which never leaves this module.
/// <c>OrderRepository.GetByCreatorSlugAsync</c> re-states this exact boolean inline in its LINQ
/// <c>Where</c> clause instead of calling this method, because EF Core cannot translate a call to
/// an extracted predicate into SQL. Keep the two in sync — this type exists so the algorithm has
/// one place to be unit-tested without a database.
/// </summary>
public static class OrderKeysetCursor
{
    /// <summary>True when (createdAt, publicId) sorts strictly after (cursorCreatedAt, cursorPublicId)
    /// in a newest-first (descending) ordering — i.e. it belongs on the next page.</summary>
    public static bool IsBeforeCursor(
        DateTimeOffset createdAt, Guid publicId, DateTimeOffset cursorCreatedAt, Guid cursorPublicId)
    {
        return createdAt < cursorCreatedAt
            || (createdAt == cursorCreatedAt && publicId.CompareTo(cursorPublicId) < 0);
    }
}
