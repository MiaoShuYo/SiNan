namespace SiNan.SDK;

public sealed class SiNanClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5043";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 200;
    public int RetryMaxDelayMs { get; set; } = 2000;
}
