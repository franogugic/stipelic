namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class Creator
{
    private Creator()
    {
    }

    private Creator(
        Guid publicId,
        int ownerUserId,
        string name,
        string slug,
        Currency defaultCurrency,
        CreatorStatus status,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        OwnerUserId = ownerUserId;
        Name = name;
        Slug = slug;
        DefaultCurrency = defaultCurrency;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Creator Create(
        int ownerUserId,
        string name,
        string slug,
        Currency defaultCurrency,
        DateTimeOffset createdAt)
    {
        return new Creator(
            Guid.NewGuid(),
            ownerUserId,
            name,
            slug,
            defaultCurrency,
            CreatorStatus.Active,
            createdAt);
    }

    public void Rename(string name, string slug, DateTimeOffset updatedAt)
    {
        Name = name;
        Slug = slug;
        UpdatedAt = updatedAt;
    }

    public void ChangeDefaultCurrency(Currency defaultCurrency, DateTimeOffset updatedAt)
    {
        DefaultCurrency = defaultCurrency;
        UpdatedAt = updatedAt;
    }

    public void Suspend(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Suspended;
        UpdatedAt = updatedAt;
    }

    public void Disable(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Disabled;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Active;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public CreatorStatus Status { get; private set; }

    public Currency DefaultCurrency { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
