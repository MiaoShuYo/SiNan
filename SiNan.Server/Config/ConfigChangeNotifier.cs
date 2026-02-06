using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SiNan.Server.Config;

public sealed class ConfigChangeNotifier
{
    private readonly ConcurrentDictionary<string, long> _versions = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();

    public long GetVersion(string key)
    {
        return _versions.GetOrAdd(key, 0);
    }

    public void Notify(string key)
    {
        _versions.AddOrUpdate(key, 1, (_, current) => current + 1);

        var waiter = _waiters.GetOrAdd(key, _ => CreateWaiter());
        waiter.TrySetResult(true);
        _waiters[key] = CreateWaiter();
    }

    public async Task<bool> WaitForChangeAsync(string key, long currentVersion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var version = _versions.GetOrAdd(key, 0);
        if (version != currentVersion)
        {
            return true;
        }

        var waiter = _waiters.GetOrAdd(key, _ => CreateWaiter());
        version = _versions.GetOrAdd(key, 0);
        if (version != currentVersion)
        {
            return true;
        }

        var delayTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(waiter.Task, delayTask);
        return completed == waiter.Task;
    }

    private static TaskCompletionSource<bool> CreateWaiter()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
