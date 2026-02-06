namespace SiNan.Server.Config;

public sealed class ConfigHistoryCleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 10;
    public int MaxVersionsPerKey { get; set; } = 20;
    public int MaxAgeDays { get; set; } = 30;
}
