using System.Collections.Concurrent;

namespace MdmBilling.Services;

public record ServerProbe(bool Ok, int LatencyMs, DateTime At, string? Error);

// Keeps the last connection test of each server, so the dashboard
// does not test the production servers on every page view.
public class ServerHealthCache
{
    private readonly ConcurrentDictionary<int, ServerProbe> _items = new();

    // Only one person at a time runs the tests.
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public bool TryGet(int serverId, out ServerProbe? probe) => _items.TryGetValue(serverId, out probe);

    public void Set(int serverId, ServerProbe probe) => _items[serverId] = probe;

    public bool IsStale(int serverId, TimeSpan maxAge) =>
        !_items.TryGetValue(serverId, out var p) || DateTime.UtcNow - p.At > maxAge;
}