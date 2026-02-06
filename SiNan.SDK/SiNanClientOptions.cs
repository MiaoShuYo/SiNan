namespace SiNan.SDK;

public sealed class SiNanClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5043";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
