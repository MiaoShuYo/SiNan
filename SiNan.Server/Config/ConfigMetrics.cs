using System.Diagnostics.Metrics;

namespace SiNan.Server.Config;

public static class ConfigMetrics
{
    private static readonly Meter Meter = new("SiNan.Config");
    private static readonly Counter<long> ChangeCounter = Meter.CreateCounter<long>("sinan.config.changes");
    private static readonly Counter<long> SubscribeRequestCounter = Meter.CreateCounter<long>("sinan.config.subscribe.requests");
    private static readonly Counter<long> SubscribeTimeoutCounter = Meter.CreateCounter<long>("sinan.config.subscribe.timeouts");
    private static readonly Counter<long> SubscribeChangeCounter = Meter.CreateCounter<long>("sinan.config.subscribe.changes");

    public static void RecordChange()
    {
        ChangeCounter.Add(1);
    }

    public static void RecordSubscribeRequest()
    {
        SubscribeRequestCounter.Add(1);
    }

    public static void RecordSubscribeTimeout()
    {
        SubscribeTimeoutCounter.Add(1);
    }

    public static void RecordSubscribeChange()
    {
        SubscribeChangeCounter.Add(1);
    }
}
