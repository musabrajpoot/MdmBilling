using System.Diagnostics;
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.Services.Udil;
using MdmBilling.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly IUdilReader _udil;
    private readonly ServerHealthCache _cache;
    private readonly int _cacheSeconds;
    private readonly bool _useFake;

    public DashboardService(AppDbContext db, IUdilReader udil, ServerHealthCache cache, IConfiguration config)
    {
        _db = db;
        _udil = udil;
        _cache = cache;
        _cacheSeconds = Math.Max(5, config.GetValue<int?>("Dashboard:HealthCacheSeconds") ?? 30);
        _useFake = config.GetValue<bool>("Udil:UseFake");
    }

    // ---------------- Page data (no server tests here, so the page opens fast) ----------------

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        var since7 = DateTime.UtcNow.AddDays(-7);

        var vm = new DashboardViewModel
        {
            RefreshSeconds = _cacheSeconds,
            UsesFakeData = _useFake,
            TotalBatches = await _db.BillingBatches.CountAsync(),
            ActiveRuns = await _db.BatchProcessingRuns
                .CountAsync(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Running),
            RunsLast7Days = await _db.BatchProcessingRuns.CountAsync(r => r.QueuedAt >= since7),
            FailedRunsLast7Days = await _db.BatchProcessingRuns
                .CountAsync(r => r.QueuedAt >= since7 && r.Status == RunStatus.Failed)
        };

        vm.RecentRuns = await _db.BatchProcessingRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(6)
            .Select(r => new RecentRunRow
            {
                RunId = r.Id,
                BatchId = r.BatchId,
                BatchNumber = r.Batch!.BatchNumber,
                Status = r.Status,
                QueuedAt = r.QueuedAt,
                EndedAt = r.EndedAt,
                TotalMeters = r.TotalMeters,
                OnlineCount = r.OnlineCount,
                OfflineCount = r.OfflineCount,
                DataAvailableCount = r.DataAvailableCount,
                DataMissingCount = r.DataMissingCount,
                NotFoundCount = r.NotFoundCount,
                NotCheckedCount = r.NotCheckedCount,
                ServersTotal = r.ServersTotal,
                ServersFailed = r.ServersFailed
            })
            .ToListAsync();

        vm.LatestRun = vm.RecentRuns.FirstOrDefault();

        vm.RecentErrors = await (from e in _db.ErrorLogs.AsNoTracking()
                                 join s in _db.Servers on e.ServerId equals (int?)s.Id into sj
                                 from s in sj.DefaultIfEmpty()
                                 orderby e.Id descending
                                 select new DashboardErrorRow
                                 {
                                     Time = e.Timestamp,
                                     RunId = e.RunId,
                                     Server = s != null ? s.Name : null,
                                     Message = e.Message
                                 })
                                .Take(5)
                                .ToListAsync();

        return vm;
    }

    // ---------------- Server health (called by the page with JavaScript) ----------------

    public async Task<ServerHealthResult> GetServerHealthAsync(bool refresh, CancellationToken ct = default)
    {
        var servers = await _db.Servers.AsNoTracking()
            .Include(s => s.Group)
            .Include(s => s.Connections)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        // 1. Test the servers whose last test is missing or old (or all, if the user pressed Refresh).
        var maxAge = TimeSpan.FromSeconds(_cacheSeconds);
        await _cache.Gate.WaitAsync(ct);
        try
        {
            var due = servers.Where(s => s.IsActive && (refresh || _cache.IsStale(s.Id, maxAge))).ToList();
            if (due.Count > 0)
            {
                using var limit = new SemaphoreSlim(4); // at most 4 tests at the same time
                await Task.WhenAll(due.Select(async s =>
                {
                    await limit.WaitAsync(ct);
                    try { _cache.Set(s.Id, await ProbeAsync(s, ct)); }
                    finally { limit.Release(); }
                }));
            }
        }
        finally
        {
            _cache.Gate.Release();
        }

        // 2. Processing history per server (last 30 days).
        var now = DateTime.UtcNow;
        var since30 = now.AddDays(-30);
        var since7 = now.AddDays(-7);

        var stats = await _db.ServerRunStatuses.AsNoTracking()
            .Where(s => s.Run!.QueuedAt >= since30)
            .Select(s => new
            {
                s.ServerId, s.RunId, s.State, s.DurationMs, s.ErrorMessage,
                Queued = s.Run!.QueuedAt, Ended = s.Run.EndedAt
            })
            .ToListAsync(ct);
        var byServer = stats.ToLookup(x => x.ServerId);

        // 3. Build one row per server.
        var rows = servers.Select(s =>
        {
            var conn = s.Connections.FirstOrDefault(c => c.IsActive) ?? s.Connections.FirstOrDefault();
            var mine = byServer[s.Id].OrderByDescending(x => x.RunId).ToList();
            var last = mine.FirstOrDefault();
            var week = mine.Where(x => x.Queued >= since7).ToList();
            var okTimes = week.Where(x => x.State == ServerRunState.Succeeded).Select(x => x.DurationMs).ToList();
            _cache.TryGet(s.Id, out var probe);

            var status = !s.IsActive ? "inactive"
                       : probe is null ? "unknown"
                       : probe.Ok ? "online" : "offline";

            return new ServerHealthRow
            {
                ServerId = s.Id,
                Name = s.Name,
                Group = s.Group?.Name,
                IpAddress = s.IpAddress,
                Host = conn?.Host ?? "",
                Port = conn?.Port ?? 0,
                Engine = conn?.DbEngine ?? "",
                Database = conn?.DatabaseName ?? "",
                IsActive = s.IsActive,
                Status = status,
                LatencyMs = probe is { Ok: true } ? probe.LatencyMs : null,
                CheckedAt = probe is null ? null : DateTime.SpecifyKind(probe.At, DateTimeKind.Utc),
                Error = probe is { Ok: false } ? probe.Error : null,
                LastRunState = last?.State.ToString(),
                LastRunAt = last is null ? null : DateTime.SpecifyKind(last.Ended ?? last.Queued, DateTimeKind.Utc),
                LastRunDurationMs = last?.DurationMs,
                LastRunError = last?.ErrorMessage,
                Runs7d = week.Count,
                Failed7d = week.Count(x => x.State == ServerRunState.Failed),
                AvgDurationMs = okTimes.Count > 0 ? (int)okTimes.Average() : null
            };
        }).ToList();

        var online = rows.Where(r => r.Status == "online").ToList();
        return new ServerHealthResult
        {
            Servers = rows,
            Online = online.Count,
            Offline = rows.Count(r => r.Status == "offline"),
            Inactive = rows.Count(r => r.Status == "inactive"),
            Unknown = rows.Count(r => r.Status == "unknown"),
            AvgLatencyMs = online.Count > 0 ? (int)online.Average(r => r.LatencyMs ?? 0) : null,
            CheckedAt = DateTime.UtcNow
        };
    }

    // One read-only connection test with a time limit.
    private async Task<ServerProbe> ProbeAsync(Server server, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var sw = Stopwatch.StartNew();

        try
        {
            var ok = await _udil.TestConnectionAsync(server, cts.Token);
            sw.Stop();
            return new ServerProbe(ok, (int)sw.ElapsedMilliseconds, DateTime.UtcNow,
                ok ? null : "Connection failed. See the error log.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServerProbe(false, 10000, DateTime.UtcNow, "Timed out after 10 seconds.");
        }
        catch (Exception ex)
        {
            return new ServerProbe(false, (int)sw.ElapsedMilliseconds, DateTime.UtcNow, ex.Message);
        }
    }
}