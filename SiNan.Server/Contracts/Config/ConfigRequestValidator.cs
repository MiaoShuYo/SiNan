using System.Collections.Generic;

namespace SiNan.Server.Contracts.Config;

public static class ConfigRequestValidator
{
    private const int MaxKeyLength = 256;
    private const int MaxContentLength = 65535;
    private const int MaxContentTypeLength = 64;

    public static IReadOnlyList<string> ValidateUpsert(ConfigUpsertRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Namespace))
        {
            errors.Add("Namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Group))
        {
            errors.Add("Group is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            errors.Add("Key is required.");
        }
        else if (request.Key.Length > MaxKeyLength)
        {
            errors.Add("Key length exceeds limit.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors.Add("Content is required.");
        }
        else if (request.Content.Length > MaxContentLength)
        {
            errors.Add("Content length exceeds limit.");
        }

        if (request.ContentType is not null && request.ContentType.Length > MaxContentTypeLength)
        {
            errors.Add("ContentType length exceeds limit.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateKey(string @namespace, string group, string key)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(@namespace))
        {
            errors.Add("Namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            errors.Add("Group is required.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add("Key is required.");
        }

        return errors;
    }
}
