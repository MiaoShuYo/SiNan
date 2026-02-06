using System.Collections.Generic;

namespace SiNan.Server.Contracts.Registry;

public static class RegistryRequestValidator
{
    public const int MinPort = 1;
    public const int MaxPort = 65535;
    public const int MinWeight = 1;
    public const int MaxWeight = 10000;
    public const int MinTtlSeconds = 5;
    public const int MaxTtlSeconds = 3600;

    public static List<string> Validate(RegisterInstanceRequest request)
    {
        var errors = new List<string>();

        ValidateCommon(request.Namespace, request.Group, request.ServiceName, request.Host, request.Port, errors);

        if (request.Weight < MinWeight || request.Weight > MaxWeight)
        {
            errors.Add($"Weight must be between {MinWeight} and {MaxWeight}.");
        }

        if (request.TtlSeconds < MinTtlSeconds || request.TtlSeconds > MaxTtlSeconds)
        {
            errors.Add($"TtlSeconds must be between {MinTtlSeconds} and {MaxTtlSeconds}.");
        }

        return errors;
    }

    public static List<string> Validate(DeregisterInstanceRequest request)
    {
        var errors = new List<string>();

        ValidateCommon(request.Namespace, request.Group, request.ServiceName, request.Host, request.Port, errors);

        return errors;
    }

    public static List<string> Validate(HeartbeatRequest request)
    {
        var errors = new List<string>();

        ValidateCommon(request.Namespace, request.Group, request.ServiceName, request.Host, request.Port, errors);

        return errors;
    }

    private static void ValidateCommon(string @namespace, string group, string serviceName, string host, int port, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            errors.Add("Namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            errors.Add("Group is required.");
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            errors.Add("ServiceName is required.");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            errors.Add("Host is required.");
        }

        if (port < MinPort || port > MaxPort)
        {
            errors.Add($"Port must be between {MinPort} and {MaxPort}.");
        }
    }
}
