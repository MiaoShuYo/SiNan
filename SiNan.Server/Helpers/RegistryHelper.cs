using SiNan.Server.Contracts.Registry;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Helpers;

public static class RegistryHelper
{
    public static string BuildServiceKey(string @namespace, string group, string serviceName)
    {
        return $"{@namespace}::{group}::{serviceName}";
    }

    public static string BuildEtag(ServiceEntity service, IReadOnlyList<ServiceInstanceEntity> instances)
    {
        var latestUpdate = instances.Count == 0
            ? service.UpdatedAt
            : instances.Max(i => i.UpdatedAt);
        return $"\"{service.Id}:{latestUpdate.ToUnixTimeMilliseconds()}:{instances.Count}\"";
    }

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
