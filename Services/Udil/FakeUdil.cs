using MdmBilling.Data;
using MdmBilling.Models;
using Microsoft.EntityFrameworkCore;
namespace MdmBilling.Services.Udil;

internal static class FakeHash
{
    // The same text always gives the same number, so fake results are repeatable.
    public static uint Of(string text)
    {
        uint h = 17;
        foreach (var ch in text) h = unchecked(h * 31 + ch);
        return h;
    }
}
public class FakeMeterServerLocator : IMeterServerLocator
{
    private readonly AppDbContext _db;
    public FakeMeterServerLocator(AppDbContext db) => _db = db;
    public async Task<Dictionary<string, int>>
    FindServersAsync(IReadOnlyList<string> meterNumbers, CancellationToken ct)
    {
        var ids = await _db.Servers.AsNoTracking()
        .Where(s => s.IsActive).OrderBy(s => s.Id).Select(s =>
        s.Id).ToListAsync(ct);
        var map = new Dictionary<string, int>
        (StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0) return map;
        foreach (var meter in meterNumbers)
        {
            var h = FakeHash.Of(meter);
            if (h % 40 == 0) continue; // about 2 to 3 percent are on no server
            map[meter] = ids[(int)(h % (uint)ids.Count)]; // spread meters over the active servers
        }
        return map;
    }
}
public class FakeUdilReader : IUdilReader
{
    public async Task<bool> TestConnectionAsync(Server server,
    CancellationToken ct)
    {
        await Task.Delay(200, ct);
        return true;
    }
    public async Task<ServerCheckResult> CheckMetersAsync(Server server,
    IReadOnlyList<string> meterNumbers, CancellationToken ct)
    {
        await Task.Delay(300, ct); // pretend network time
                                   // To test the failure path, name a test server "FAIL-1".
        if (server.Name.StartsWith("FAIL",
        StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test failure (fake).");
        var list = new List<MeterCheck>(meterNumbers.Count);
        foreach (var meter in meterNumbers)
        {
            var n = (int)(FakeHash.Of(meter + "#") % 100);
            if (n < 2)
            {
                list.Add(new MeterCheck(meter, false,
                OnlineStatus.NotChecked, DataStatus.NotChecked, null));
                continue;
            }
            list.Add(new MeterCheck(meter, true,
            n < 60 ? OnlineStatus.Online : OnlineStatus.Offline,
            n < 40 ? DataStatus.Available : DataStatus.Missing,
            null));
        }
        return new ServerCheckResult(server.Id, true, null, list);
    }
}
