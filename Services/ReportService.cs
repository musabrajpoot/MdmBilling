
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.Services.Reports;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Services;

public record ReportDownload(
    Stream Content,
    string ContentType,
    string FileName
);

public class ReportService
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;
    private readonly ExcelReportWriter _excel;
    private readonly PdfReportWriter _pdf;
    private readonly AuditService _audit;

    public ReportService(
        AppDbContext db,
        FileStorage storage,
        ExcelReportWriter excel,
        PdfReportWriter pdf,
        AuditService audit)
    {
        _db = db;
        _storage = storage;
        _excel = excel;
        _pdf = pdf;
        _audit = audit;
    }

    public async Task<ReportDownload> GetAsync(
        int runId,
        ReportFormat format,
        string userId)
    {
        var run = await _db.BatchProcessingRuns
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new
            {
                r.Status,
                BatchNumber = r.Batch!.BatchNumber
            })
            .FirstOrDefaultAsync();

        if (run is null)
        {
            throw new InvalidOperationException("Run not found.");
        }

        if (run.Status is not (
            RunStatus.Completed or RunStatus.CompletedWithErrors))
        {
            throw new InvalidOperationException(
                "Reports are available only for completed runs."
            );
        }

        var ext = format == ReportFormat.Excel ? ".xlsx" : ".pdf";

        var type = format == ReportFormat.Excel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf";

        var safe = new string(
            run.BatchNumber
                .Where(char.IsLetterOrDigit)
                .ToArray()
        );

        var fileName =
            $"Batch_{(safe == "" ? "report" : safe)}_Run{runId}{ext}";

        // Reuse the saved report if it already exists.
        var existing = await _db.GeneratedReports
            .AsNoTracking()
            .Where(r => r.RunId == runId && r.Format == format)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (existing is not null &&
            _storage.Exists(existing.StoredPath))
        {
            await _audit.LogAsync(
                "ReportDownloaded",
                "BatchProcessingRun",
                runId.ToString(),
                userId
            );

            return new ReportDownload(
                _storage.Open(existing.StoredPath),
                type,
                fileName
            );
        }

        // Build report using saved processing results.
        var data = await BuildDataAsync(runId);

        var bytes = format == ReportFormat.Excel
            ? _excel.Write(data)
            : _pdf.Write(data);

        // Save the generated file.
        using (var ms = new MemoryStream(bytes))
        {
            var path = await _storage.SaveAsync(
                ms,
                ext,
                "reports"
            );

            _db.GeneratedReports.Add(new GeneratedReport
            {
                RunId = runId,
                Format = format,
                StoredPath = path,
                GeneratedById = userId
            });

            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync(
            "ReportGenerated",
            "BatchProcessingRun",
            runId.ToString(),
            userId
        );

        return new ReportDownload(
            new MemoryStream(bytes),
            type,
            fileName
        );
    }

    private async Task<ReportData> BuildDataAsync(int runId)
    {
        var run = await _db.BatchProcessingRuns
            .AsNoTracking()
            .Include(r => r.Batch)
            .FirstAsync(r => r.Id == runId);

        var rows = await _db.MeterRows(runId)
            .OrderBy(x => x.MeterNumber)
            .ToListAsync();

        var statuses = await _db.ServerRunStatuses
    .AsNoTracking()
    .Where(s => s.RunId == runId)
    .Select(s => new
    {
        ServerName = s.Server!.Name,
        GroupName = s.Server.Group != null ? s.Server.Group.Name : "",
        s.State,
        s.ErrorMessage
    })
    .ToListAsync();

        var servers = statuses
            .OrderBy(s => s.ServerName)
            .Select(s =>
            {
                var mine = rows
                            .Where(r => r.ServerName == s.ServerName)
                            .ToList();

                return new ServerSummaryRow
                {
                    Group = s.GroupName,
                    Server = s.ServerName,
                    State = s.State.ToString(),
                    Error = s.ErrorMessage,
                    Meters = mine.Count,
                    Online = mine.Count(
                r => r.Online == OnlineStatus.Online
            ),
                    Offline = mine.Count(
                r => r.Online == OnlineStatus.Offline
            ),
                    DataAvailable = mine.Count(
                r => r.Data == DataStatus.Available
            ),
                    DataMissing = mine.Count(
                r => r.Data == DataStatus.Missing
            )
                };
                // unchanged from your code
            })
            .ToList();

        return new ReportData
        {
            BatchNumber = run.Batch!.BatchNumber,
            BillingDate = run.Batch.BillingDate,
            RunId = run.Id,
            RunDate = (run.EndedAt ?? run.QueuedAt).ToLocalTime(),

            OverallStatus =
                run.Status == RunStatus.CompletedWithErrors
                    ? $"Completed with errors ({run.ServersFailed} server(s) failed)"
                    : run.Status.ToString(),

            TotalMeters = run.TotalMeters,
            Online = run.OnlineCount,
            Offline = run.OfflineCount,
            DataAvailable = run.DataAvailableCount,
            DataMissing = run.DataMissingCount,
            NotFound = run.NotFoundCount,
            NotChecked = run.NotCheckedCount,

            Rows = rows,
            Servers = servers
        };
    }
}
