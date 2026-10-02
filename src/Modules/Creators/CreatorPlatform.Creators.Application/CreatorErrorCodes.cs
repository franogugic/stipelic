namespace CreatorPlatform.Creators.Application;

/// <summary>Machine-readable error codes returned by the creator workspace endpoints.</summary>
public static class CreatorErrorCodes
{
    /// <summary>409 on POST /api/creators: the requested URL (slug) belongs to another active workspace.</summary>
    public const string SlugTaken = "CREATOR_SLUG_TAKEN";

    /// <summary>409 on POST /api/creators: the user already owns an active workspace.</summary>
    public const string AlreadyExists = "CREATOR_ALREADY_EXISTS";
}
