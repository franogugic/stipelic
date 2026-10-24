namespace CreatorPlatform.Media.Domain.Media;

public enum BlobReferenceStatus
{
    Pending,
    Confirmed,

    /// <summary>Never confirmed: the maintenance job deleted its blob. The row stays until the daily upload budget's
    /// window has passed, so an abandoned upload still counts against that budget, then it is deleted too.</summary>
    Abandoned
}
