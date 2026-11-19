using System.Collections.Concurrent;
using System.Diagnostics;
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.Services.Udil;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MdmBilling.Services;

public class ProcessingService
{
    private readonly AppDbContext _db;
    private readonly IUdilReader _udil;
    private readonly ProcessingOptions _opt;
    private readonly ILogger<ProcessingService> _log;

    public ProcessingService(
        AppDbContext db,
        IUdilReader udil,
        IOptions<ProcessingOptions> options,
        ILogger<ProcessingService> log)
    {
        _db = db;
        _udil = udil;
        _opt = options.Value;
        _log = log;
    }

    public async Task ProcessRunAsync(int runId, CancellationToken ct)
    {
        var run = await _db.BatchProcessingRuns.FirstAsync(r => r.Id == runId, ct);
        run.Status = RunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var meters = await _db.BatchMeters.AsNoTracking()
            .Where(m => m.BatchId == run.BatchId)
            .Select(m => new { m.Id, Number = m.MeterNumber })
            .ToListAsync(ct);
        var numbers = meters.Select(m => m.Number).ToList();

        // The servers the user selected when the batch was uploaded (active ones only).
        var servers = await _db.Servers.AsNoTracking()
            .Include(s => s.Connections)
            .Where(s => s.IsActive && _db.BatchServers.Any(bs => bs.BatchId == run.BatchId && bs.ServerId == s.Id))
            .ToListAsync(ct);

        if (servers.Count == 0)
        {
            run.Status = RunStatus.Failed;
            run.EndedAt = DateTime.UtcNow;
            _db.ErrorLogs.Add(new ErrorLog { RunId = run.Id, Message = "No active server is selected for this batch." });
            await _db.SaveChangesAsync(ct);
            return;
        }

        run.ServersTotal = servers.Count;
        foreach (var s in servers)
            _db.ServerRunStatuses.Add(new ServerRunStatus { RunId = run.Id, ServerId = s.Id });
        await _db.SaveChangesAsync(ct);
        var statusRows = await _db.ServerRunStatuses.Where(x => x.RunId == run.Id).ToListAsync(ct);

        // Ask each selected server about the meters, in parallel, with a limit.
        var results = new ConcurrentDictionary<int, ServerCheckResult>();
        using var gate = new SemaphoreSlim(Math.Max(1, _opt.MaxParallelServers));
        using var dbLock = new SemaphoreSlim(1, 1); // one DbContext, so database updates go one at a time

        var tasks = servers.Select(async server =>
        {
            await gate.WaitAsync(ct);
            var sw = Stopwatch.StartNew();
            ServerCheckResult result;
            try { result = await CheckServerAsync(server, numbers, ct); }
            finally { gate.Release(); }
            sw.Stop();
            results[server.Id] = result;

            await dbLock.WaitAsync(ct);
            try
            {
                var row = statusRows.First(x => x.ServerId == server.Id);
                row.State = result.Succeeded ? ServerRunState.Succeeded : ServerRunState.Failed;
                row.ErrorMessage = result.Error;
                row.DurationMs = (int)sw.ElapsedMilliseconds;
                run.ServersDone++;
                if (!result.Succeeded)
                {
                    run.ServersFailed++;
                    _db.ErrorLogs.Add(new ErrorLog
                    {
                        RunId = run.Id,
                        ServerId = server.Id,
                        Message = result.Error ?? "Server check failed"
                    });
                }
                await _db.SaveChangesAsync(ct);
            }
            finally { dbLock.Release(); }
        }).ToList();

        await Task.WhenAll(tasks);

        // Combine: which selected servers found each meter?
        var failedServers = results.Values.Count(r => !r.Succeeded);
        var hits = results.Values.Where(r => r.Succeeded)
            .SelectMany(r => r.Meters.Where(m => m.Found).Select(m => (r.ServerId, Meter: m)))
            .GroupBy(x => x.Meter.MeterNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.ServerId).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var m in meters)
        {
            var map = new MeterServerMapping { RunId = run.Id, BatchMeterId = m.Id };
            var st = new MeterStatusResult { RunId = run.Id, BatchMeterId = m.Id };
            var da = new DailyDataAvailability { RunId = run.Id, BatchMeterId = m.Id };

            if (hits.TryGetValue(m.Number, out var list))
            {
                var first = list[0];
                map.Found = true;
                map.ServerId = first.ServerId;
                st.OnlineStatus = first.Meter.Online;
                st.Remarks = list.Count > 1
                    ? $"Found on {list.Count} servers; the first one was used."
                    : first.Meter.Remarks;
                da.DataStatus = first.Meter.Data;
                da.MissingDetails = first.Meter.Data == DataStatus.Missing ? first.Meter.Remarks : null;

                if (first.Meter.Online == OnlineStatus.Online) run.OnlineCount++;
                else if (first.Meter.Online == OnlineStatus.Offline) run.OfflineCount++;
                if (first.Meter.Data == DataStatus.Available) run.DataAvailableCount++;
                else if (first.Meter.Data == DataStatus.Missing) run.DataMissingCount++;
            }
            else if (failedServers > 0)
            {
                st.OnlineStatus = OnlineStatus.NotChecked;
                da.DataStatus = DataStatus.NotChecked;
                st.Remarks = $"Not found on the servers that answered. {failedServers} server(s) could not be checked.";
                run.NotCheckedCount++;
            }
            else
            {
                st.OnlineStatus = OnlineStatus.Unknown;
                da.DataStatus = DataStatus.Unknown;
                st.Remarks = "Not found on the selected servers.";
                run.NotFoundCount++;
            }

            _db.MeterServerMappings.Add(map);
            _db.MeterStatusResults.Add(st);
            _db.DailyDataAvailabilities.Add(da);
        }

        run.Status = failedServers == 0 ? RunStatus.Completed
                   : failedServers == servers.Count ? RunStatus.Failed
                   : RunStatus.CompletedWithErrors;
        run.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Run {RunId} finished as {Status}: {Servers} servers, {Failed} failed",
            run.Id, run.Status, servers.Count, failedServers);
    }

    // Checks one server's meters in chunks, with retries.
    private async Task<ServerCheckResult> CheckServerAsync(
        Server server,
        List<string> meters,
        CancellationToken ct)
    {
        var all = new List<MeterCheck>(meters.Count);

        foreach (var chunk in meters.Chunk(Math.Max(1, _opt.ChunkSize)))
        {
            ServerCheckResult? successfulResult = null;
            string? error = null;

            // Retry failed requests.
            for (var attempt = 1;
                 attempt <= _opt.MaxRetries + 1 && successfulResult is null;
                 attempt++)
            {
                try
                {
                    var result = await _udil.CheckMetersAsync(server, chunk, ct);

                    if (result.Succeeded)
                    {
                        successfulResult = result;
                    }
                    else
                    {
                        error = result.Error;

                        _log.LogWarning(
                            "Server {ServerId} check failed on attempt {Attempt}: {Error}",
                            server.Id, attempt, error);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex.Message;

                    _log.LogWarning(ex,
                        "Error checking server {ServerId}, attempt {Attempt}",
                        server.Id, attempt);
                }

                // Use the configured retry delay, increasing it per attempt.
                if (successfulResult is null && attempt <= _opt.MaxRetries)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_opt.RetryDelaySeconds * attempt),
                        ct);
                }
            }

            // ServerCheckResult is a positional record: all four arguments are required.
            if (successfulResult is null)
            {
                return new ServerCheckResult(
                    server.Id,
                    false,
                    error ?? "Server check failed after retries.",
                    Array.Empty<MeterCheck>());
            }

            all.AddRange(successfulResult.Meters);
        }

        return new ServerCheckResult(server.Id, true, null, all);
    }
}