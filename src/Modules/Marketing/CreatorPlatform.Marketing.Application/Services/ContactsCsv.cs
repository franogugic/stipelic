using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Shared.Application.Csv;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>The subscribers export format: <c>email,sources,joined_at,status</c>.</summary>
public static class ContactsCsv
{
    public static readonly string Header = CsvFormatter.Row("email", "sources", "joined_at", "status");

    /// <summary>Sources are every source page title in first-capture order joined with "; "; joined_at is the
    /// first capture in ISO 8601 UTC; status is active or unsubscribed.</summary>
    public static string Row(ContactDto contact) => CsvFormatter.Row(
        contact.Email,
        string.Join("; ", contact.SourceList.Select(source => source.Title)),
        CsvFormatter.Timestamp(contact.FirstCapturedAt),
        contact.IsUnsubscribed ? "unsubscribed" : "active");

    public static string FileName(string slug, DateTimeOffset now) =>
        $"subscribers-{slug}-{now.UtcDateTime:yyyy-MM-dd}.csv";
}
