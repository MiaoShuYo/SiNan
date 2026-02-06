namespace SiNan.Server.Contracts.Common;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string ServiceNotFound = "service_not_found";
    public const string InstanceNotFound = "instance_not_found";
    public const string ConfigNotFound = "config_not_found";
    public const string ConfigAlreadyExists = "config_already_exists";
    public const string ConfigHistoryNotFound = "config_history_not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
}
