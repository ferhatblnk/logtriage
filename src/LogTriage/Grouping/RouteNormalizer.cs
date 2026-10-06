namespace LogTriage.Grouping;

/// <summary>
/// Derives a route from a raw path when the service does not log its route template:
/// numeric, GUID and long hexadecimal segments become <c>{id}</c>, so
/// <c>/orders/42/items</c> and <c>/orders/43/items</c> land in the same group.
/// </summary>
public static class RouteNormalizer
{
    private const string Placeholder = "{id}";
    private const int MinHexLength = 16;

    public static string Normalize(string path)
    {
        var end = path.IndexOfAny(['?', '#']);
        var segments = (end < 0 ? path : path[..end])
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => IsIdentifier(segment) ? Placeholder : segment);

        return "/" + string.Join('/', segments);
    }

    private static bool IsIdentifier(string segment) =>
        segment.All(char.IsAsciiDigit)
        || Guid.TryParse(segment, out _)
        || (segment.Length >= MinHexLength && segment.All(char.IsAsciiHexDigit));
}
