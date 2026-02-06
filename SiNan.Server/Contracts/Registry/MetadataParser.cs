using System.Collections.Generic;
using System.Text.Json;

namespace SiNan.Server.Contracts.Registry;

public static class MetadataParser
{
    public static Dictionary<string, string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return result ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
