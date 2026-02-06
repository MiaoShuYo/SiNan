namespace SiNan.Server.Quotas;

public sealed class QuotaOptions
{
    public int MaxServicesPerNamespace { get; set; } = 0;
    public int MaxInstancesPerNamespace { get; set; } = 0;
    public int MaxConfigsPerNamespace { get; set; } = 0;
    public int MaxConfigContentLength { get; set; } = 0;
}
