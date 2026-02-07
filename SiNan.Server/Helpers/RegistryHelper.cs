/// <summary>
/// Service registry helper class
/// Provides utility methods for service key building, ETag generation, and response transformation
/// </summary>

using SiNan.Server.Contracts.Registry;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Helpers;

public static class RegistryHelper
{
    /// <summary>
    /// Builds a unique identifier key for a service
    /// </summary>
    /// <param name="namespace">Namespace</param>
    /// <param name="group">Group</param>
    /// <param name="serviceName">Service name</param>
    /// <returns>Service key in format "namespace::group::serviceName"</returns>
    public static string BuildServiceKey(string @namespace, string group, string serviceName)
    {
        return $"{@namespace}::{group}::{serviceName}";
    }

    /// <summary>
    /// Generates ETag value for service instance list
    /// ETag is used for long-polling mechanism to detect changes in instance list
    /// </summary>
    /// <param name="service">Service entity</param>
    /// <param name="instances">Instance list</param>
    /// <returns>ETag string in format "serviceId:latestUpdateTimestamp:instanceCount"</returns>
    public static string BuildEtag(ServiceEntity service, IReadOnlyList<ServiceInstanceEntity> instances)
    {
        // Calculate latest update time: use max instance update time if instances exist, otherwise use service update time
        var latestUpdate = instances.Count == 0
            ? service.UpdatedAt
            : instances.Max(i => i.UpdatedAt);
        return $"\"{service.Id}:{latestUpdate.ToUnixTimeMilliseconds()}:{instances.Count}\"";
    }

    /// <summary>
    /// Converts service instance entity list to API response object
    /// </summary>
    /// <param name="namespace">Namespace</param>
    /// <param name="group">Group</param>
    /// <param name="serviceName">Service name</param>
    /// <param name="instances">Instance list</param>
    /// <param name="etagValue">ETag value</param>
    /// <returns>Service instances response object</returns>
    public static ServiceInstancesResponse BuildInstancesResponse(
        string @namespace,
        string group,
        string serviceName,
        IReadOnlyList<ServiceInstanceEntity> instances,
        string etagValue)
    {
        return new ServiceInstancesResponse
        {
            Namespace = @namespace,
            Group = group,
            ServiceName = serviceName,
            ETag = etagValue,
            Instances = instances.Select(instance => new ServiceInstanceDto
            {
                InstanceId = instance.InstanceId,
                Host = instance.Host,
                Port = instance.Port,
                Weight = instance.Weight,
                Healthy = instance.Healthy,
                TtlSeconds = instance.TtlSeconds,
                IsEphemeral = instance.IsEphemeral,
                Metadata = MetadataParser.Parse(instance.MetadataJson)
            }).ToList()
        };
    }
}
