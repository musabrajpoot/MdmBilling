using MdmBilling.Data;
using MdmBilling.Models;
using Microsoft.EntityFrameworkCore;
namespace MdmBilling.Services;

public class ProcessingWorker : BackgroundService
{
    private readonly ProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ProcessingWorker> _log;
    public ProcessingWorker(ProcessingQueue queue, IServiceScopeFactory
    scopes, ILogger<ProcessingWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }
    protected override async Task ExecuteAsync(CancellationToken
    stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            int runId;
            try { runId = await _queue.DequeueAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            try
            {
                using var scope = _scopes.CreateScope();
                var processor =
                scope.ServiceProvider.GetRequiredService<ProcessingService>();
                await processor.ProcessRunAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) { /* the app is stopping */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "Run {RunId} failed", runId);
                await MarkFailedAsync(runId, ex.Message);
            }
        }
    }
    // After a restart: runs that were Running become Interrupted, Queued runs go back in the queue.
private async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>
            ();
            var running = await db.BatchProcessingRuns.Where(r => r.Status ==
            RunStatus.Running).ToListAsync(ct);
            foreach (var r in running)
            {
                r.Status = RunStatus.Interrupted;
                r.EndedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            var queued = await db.BatchProcessingRuns.Where(r => r.Status ==
            RunStatus.Queued)
            .OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
            foreach (var id in queued) await _queue.EnqueueAsync(id, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not recover runs at startup");
        }
    }
    private async Task MarkFailedAsync(int runId, string message)
    {
        try
        {
            using var scope = _scopes.CreateScope(); // fresh scope: the oldcontext may be in a bad state
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>
            ();
            var run = await db.BatchProcessingRuns.FirstOrDefaultAsync(r =>
            r.Id == runId);
            if (run is null) return;
            run.Status = RunStatus.Failed;
            run.EndedAt = DateTime.UtcNow;
            db.ErrorLogs.Add(new ErrorLog
            {
                RunId = runId,
                Message = message
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not mark run {RunId} as failed", runId);
        }
    }
}
