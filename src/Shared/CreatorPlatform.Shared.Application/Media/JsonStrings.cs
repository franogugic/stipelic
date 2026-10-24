using System.Text.Json;

namespace CreatorPlatform.Shared.Application.Media;

public static class JsonStrings
{
    /// <summary>Every string value anywhere in <paramref name="json"/> (not property names) — how a page section's image
    /// URLs are found whatever key or nesting the editor uses (<c>imageUrl</c>, <c>imageUrls[]</c>, <c>logoUrl</c>, …).</summary>
    public static IEnumerable<string> All(string json)
    {
        using var document = JsonDocument.Parse(json);
        var found = new List<string>();
        Collect(document.RootElement, found);
        return found;
    }

    private static void Collect(JsonElement element, List<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                found.Add(element.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Collect(property.Value, found);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Collect(item, found);
                break;
        }
    }
}
