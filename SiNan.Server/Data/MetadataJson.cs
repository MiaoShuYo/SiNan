using System.Collections.Generic;
using System.Text.Json;

namespace SiNan.Server.Data;

public static class MetadataJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static string Serialize(Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(metadata, Options);
    }
}
