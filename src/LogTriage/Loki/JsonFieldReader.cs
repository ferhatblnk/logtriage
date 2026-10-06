using System.Text.Json;

namespace LogTriage.Loki;

/// <summary>
/// Reads a dotted field name from a JSON object. The name may be a flat key (<c>"exception.type"</c>),
/// a path through nested objects (<c>exception → type</c>), or a mix (<c>State → "exception.type"</c>).
/// </summary>
internal static class JsonFieldReader
{
    public static string? Read(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || name.Length == 0)
            return null;

        if (element.TryGetProperty(name, out var direct))
            return Text(direct);

        for (var dot = name.IndexOf('.'); dot > 0; dot = name.IndexOf('.', dot + 1))
        {
            if (element.TryGetProperty(name[..dot], out var child) && Read(child, name[(dot + 1)..]) is { } nested)
                return nested;
        }

        return null;
    }

    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.GetRawText(),
    };
}
