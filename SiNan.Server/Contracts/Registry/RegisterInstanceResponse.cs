namespace SiNan.Server.Contracts.Registry;

public sealed class RegisterInstanceResponse
{
    public string InstanceId { get; set; } = string.Empty;
    public Guid ServiceId { get; set; }
}
