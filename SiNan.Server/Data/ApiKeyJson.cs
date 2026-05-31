using System.Collections.Generic;
using System.Text.Json;

namespace SiNan.Server.Data;

/// <summary>
/// Helper to serialize/deserialize JSON string arrays stored in ApiKeyEntity.
/// </summary>
public static class ApiKeyJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static string SerializeList(IEnumerable<string>? items)
    {
        if (items is null)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(items, Options);
    }

    public static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
