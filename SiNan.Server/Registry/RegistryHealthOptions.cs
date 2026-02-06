namespace SiNan.Server.Registry;

public sealed class RegistryHealthOptions
{
    public bool Enabled { get; set; } = true;
    public int CheckIntervalSeconds { get; set; } = 5;
    public int EvictionGraceSeconds { get; set; } = 30;
}
