using SiNan.Server.Contracts.Config;
using SiNan.Server.Data.Entities;
using SiNan.Server.Quotas;

namespace SiNan.Server.Helpers;

public static class ConfigHelper
{
    public static string BuildConfigKey(string @namespace, string group, string key)
    {
        return $"{@namespace}::{group}::{key}";
    }

    public static string BuildConfigEtag(ConfigItemEntity config)
    {
        var publishedAt = config.PublishedAt?.ToUnixTimeMilliseconds() ?? 0;
        return $"\"{config.Id}:{config.Version}:{publishedAt}\"";
    }

    public static ConfigItemResponse ToConfigItemResponse(ConfigItemEntity config)
    {
        return new ConfigItemResponse
        {
            Namespace = config.Namespace,
            Group = config.Group,
            Key = config.Key,
            Content = config.Content,
            ContentType = config.ContentType,
            Version = config.Version,
            PublishedAt = config.PublishedAt,
            PublishedBy = config.PublishedBy,
            UpdatedAt = config.UpdatedAt
        };
    }

    public static bool ValidateConfigContentQuota(QuotaOptions options, string content, out string message)
    {
        var maxLength = options.MaxConfigContentLength;
        if (maxLength > 0 && content.Length > maxLength)
        {
            message = "Config content length exceeds quota.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
