/// <summary>
/// Configuration management helper class
/// Provides utility methods for config key building, ETag generation, response transformation, and quota validation
/// </summary>

using SiNan.Server.Contracts.Config;
using SiNan.Server.Data.Entities;
using SiNan.Server.Quotas;

namespace SiNan.Server.Helpers;

public static class ConfigHelper
{
    /// <summary>
    /// Builds a unique identifier key for a configuration item
    /// </summary>
    /// <param name="namespace">Namespace</param>
    /// <param name="group">Group</param>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration key in format "namespace::group::key"</returns>
    public static string BuildConfigKey(string @namespace, string group, string key)
    {
        return $"{@namespace}::{group}::{key}";
    }

    /// <summary>
    /// Generates ETag value for configuration item
    /// ETag is used for long-polling mechanism to detect configuration changes
    /// </summary>
    /// <param name="config">Configuration item entity</param>
    /// <returns>ETag string in format "configId:version:publishedTimestamp"</returns>
    public static string BuildConfigEtag(ConfigItemEntity config)
    {
        var publishedAt = config.PublishedAt?.ToUnixTimeMilliseconds() ?? 0;
        return $"\"{config.Id}:{config.Version}:{publishedAt}\"";
    }

    /// <summary>
    /// Converts configuration item entity to API response object
    /// </summary>
    /// <param name="config">Configuration item entity</param>
    /// <returns>Configuration item response object</returns>
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

    /// <summary>
    /// Validates whether configuration content exceeds quota limit
    /// </summary>
    /// <param name="options">Quota options</param>
    /// <param name="content">Configuration content</param>
    /// <param name="message">Error message when validation fails</param>
    /// <returns>true if validation passes, false if quota is exceeded</returns>
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
