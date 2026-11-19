using System.Security.Cryptography;
using MdmBilling.Data;
using MdmBilling.Models;
using MdmBilling.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Services;

public class BatchService
{
    private readonly AppDbContext _db;
    private readonly ExcelBatchReader _reader;
    private readonly FileStorage _storage;
    private readonly AuditService _audit;
    private readonly ProcessingQueue _queue;

    public BatchService(
        AppDbContext db,
        ExcelBatchReader reader,
        FileStorage storage,
        AuditService audit,
        ProcessingQueue queue)
    {
        _db = db;
        _reader = reader;
        _storage = storage;
        _audit = audit;
        _queue = queue;
    }

    public async Task<UploadResult> UploadAsync(string batchNumber, DateTime? billingDate,
        IReadOnlyList<int> serverIds, string fileName, Stream file, string userId, CancellationToken ct = default)
    {
        var result = new UploadResult();

        batchNumber = batchNumber?.Trim() ?? "";

        // Validate the entered batch number on the server.
        if (!IsValidBatchNumber(batchNumber))
        {
            result.Errors.Add(
                "Batch number must contain digits only, such as 1, 2, or 3.");

            return result;
        }

        // Prevent uploading an existing batch number.
        bool batchAlreadyExists = await _db.BillingBatches
            .AnyAsync(b => b.BatchNumber == batchNumber, ct);

        if (batchAlreadyExists)
        {
            result.Errors.Add(
                $"Batch {batchNumber} already exists. Open it and start a new run instead.");

            return result;
        }

        // The servers the user selected for this batch.
        var ids = serverIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            result.Errors.Add("Select at least one server.");
            return result;
        }

        var validCount = await _db.Servers.CountAsync(s => s.IsActive && ids.Contains(s.Id), ct);
        if (validCount != ids.Count)
        {
            result.Errors.Add("One or more selected servers are not available.");
            return result;
        }

        // Copy the uploaded file into memory because it is read more than once.
        using var memoryStream = new MemoryStream();

        await file.CopyToAsync(memoryStream, ct);

        memoryStream.Position = 0;

        // Read and validate the Excel file.
        var readResult = _reader.Read(memoryStream);

        result.Errors.AddRange(readResult.Errors);
        result.Warnings.AddRange(readResult.Warnings);

        if (result.Errors.Count > 0)
        {
            return result;
        }

        if (readResult.Meters.Count == 0)
        {
            result.Errors.Add("The file has no meters.");
            return result;
        }

        if (readResult.BatchNumbers.Count == 0)
        {
            result.Errors.Add(
                "No valid batch number was found in the Excel file.");

            return result;
        }

        if (readResult.BatchNumbers.Count > 1)
        {
            result.Errors.Add(
                "The file has more than one batch number: " +
                string.Join(", ", readResult.BatchNumbers));

            return result;
        }

        var excelBatchNumber = readResult.BatchNumbers[0];

        // Compare the two numeric strings exactly.
        if (!string.Equals(
            excelBatchNumber,
            batchNumber,
            StringComparison.Ordinal))
        {
            result.Errors.Add(
                $"Batch number in the file ({excelBatchNumber}) does not match the entered batch number ({batchNumber}).");

            return result;
        }

        // Calculate the uploaded file hash.
        memoryStream.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(memoryStream, ct);

        var fileHash = Convert.ToHexString(hashBytes);

        bool fileAlreadyUploaded = await _db.UploadedBatchFiles
            .AnyAsync(f => f.FileHash == fileHash, ct);

        if (fileAlreadyUploaded)
        {
            result.Warnings.Add(
                "This exact file was uploaded before.");
        }

        // Save the original Excel file outside wwwroot.
        memoryStream.Position = 0;

        var storedPath = await _storage.SaveAsync(
            memoryStream,
            ".xlsx",
            "uploads",
            ct);

        // Create the batch record.
        var batch = new BillingBatch
        {
            BatchNumber = batchNumber,
            BillingDate = billingDate,
            CreatedById = userId
        };

        // Create the uploaded file record.
        var upload = new UploadedBatchFile
        {
            Batch = batch,
            FileName = Path.GetFileName(fileName),
            StoredPath = storedPath,
            FileHash = fileHash,
            UploadedById = userId,
            ValidationStatus = readResult.Warnings.Count == 0
                ? "Valid"
                : "Valid with warnings"
        };

        // Add meters to the batch.
        foreach (var meter in readResult.Meters)
        {
            batch.Meters.Add(new BatchMeter
            {
                Batch = batch,
                File = upload,
                MeterNumber = meter
            });
        }

        // Add the selected servers to the batch.
        foreach (var id in ids)
        {
            batch.BatchServers.Add(new BatchServer { Batch = batch, ServerId = id });
        }

        _db.BillingBatches.Add(batch);
        _db.UploadedBatchFiles.Add(upload);

        await _db.SaveChangesAsync(ct);

        // Record the upload in the audit log.
        await _audit.LogAsync(
            "BatchUploaded",
            "BillingBatch",
            batch.Id.ToString(),
            userId);

        result.BatchId = batch.Id;
        result.MeterCount = readResult.Meters.Count;

        return result;
    }

    public Task<List<BatchListItem>> GetBatchesAsync()
    {
        return _db.BillingBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(200)
            .Select(b => new BatchListItem
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                BillingDate = b.BillingDate,
                CreatedAt = b.CreatedAt,
                MeterCount = b.Meters.Count,

                LatestRunId = b.Runs
                    .OrderByDescending(r => r.Id)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefault(),

                LatestRunStatus = b.Runs
                    .OrderByDescending(r => r.Id)
                    .Select(r => (RunStatus?)r.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<BatchDetails?> GetBatchAsync(int id)
    {
        var details = await _db.BillingBatches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BatchDetails
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                BillingDate = b.BillingDate,
                CreatedAt = b.CreatedAt,
                MeterCount = b.Meters.Count,
                ServerNames = b.BatchServers
                    .Select(x => x.Server!.Name)
                    .OrderBy(n => n)
                    .ToList(),

                Runs = b.Runs
                    .OrderByDescending(r => r.Id)
                    .Select(r => new RunListItem
                    {
                        Id = r.Id,
                        QueuedAt = r.QueuedAt,
                        Status = r.Status,
                        TotalMeters = r.TotalMeters,
                        OnlineCount = r.OnlineCount,
                        OfflineCount = r.OfflineCount,
                        DataAvailableCount = r.DataAvailableCount,
                        DataMissingCount = r.DataMissingCount
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (details is not null)
        {
            details.FileName = await _db.UploadedBatchFiles
                .Where(f => f.BatchId == id)
                .Select(f => f.FileName)
                .FirstOrDefaultAsync();

            // For the server checklist on the batch page.
            details.AllServers = await GetServerOptionsAsync();
            details.SelectedServerIds = await _db.BatchServers
                .Where(x => x.BatchId == id)
                .Select(x => x.ServerId)
                .ToListAsync();
        }

        return details;
    }

    public async Task<int> StartRunAsync(int batchId, IReadOnlyList<int> serverIds, string userId)
    {
        var ids = serverIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Select at least one server.");

        var valid = await _db.Servers.CountAsync(s => s.IsActive && ids.Contains(s.Id));
        if (valid != ids.Count)
            throw new InvalidOperationException("One or more selected servers are not available.");

        var count = await _db.BatchMeters.CountAsync(m => m.BatchId == batchId);
        if (count == 0)
            throw new InvalidOperationException("This batch has no meters.");

        var active = await _db.BatchProcessingRuns.AnyAsync(r => r.BatchId == batchId &&
            (r.Status == RunStatus.Queued || r.Status == RunStatus.Running));
        if (active)
            throw new InvalidOperationException("This batch is already being processed.");

        // Save the chosen servers on the batch (add the new ones, remove the unticked ones).
        var existing = await _db.BatchServers.Where(x => x.BatchId == batchId).ToListAsync();

        foreach (var e in existing.Where(e => !ids.Contains(e.ServerId)))
            _db.BatchServers.Remove(e);

        foreach (var sid in ids.Where(sid => existing.All(e => e.ServerId != sid)))
            _db.BatchServers.Add(new BatchServer { BatchId = batchId, ServerId = sid });

        var run = new BatchProcessingRun { BatchId = batchId, StartedById = userId, TotalMeters = count };
        _db.BatchProcessingRuns.Add(run);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("RunStarted", "BatchProcessingRun", run.Id.ToString(), userId);
        await _queue.EnqueueAsync(run.Id);
        return run.Id;
    }

    public Task<RunProgress?> GetRunProgressAsync(int runId) =>
        _db.BatchProcessingRuns.AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new RunProgress
            {
                RunId = r.Id,
                BatchId = r.BatchId,
                BatchNumber = r.Batch!.BatchNumber,
                BillingDate = r.Batch.BillingDate,
                Status = r.Status,
                TotalMeters = r.TotalMeters,
                OnlineCount = r.OnlineCount,
                OfflineCount = r.OfflineCount,
                DataAvailableCount = r.DataAvailableCount,
                DataMissingCount = r.DataMissingCount,
                NotFoundCount = r.NotFoundCount,
                NotCheckedCount = r.NotCheckedCount,
                ServersTotal = r.ServersTotal,
                ServersDone = r.ServersDone,
                ServersFailed = r.ServersFailed,
                QueuedAt = r.QueuedAt,
                StartedAt = r.StartedAt,
                EndedAt = r.EndedAt
            })
            .FirstOrDefaultAsync();

    public async Task<PagedResult<MeterResultRow>> GetRunResultsAsync(
        int runId, string? filter, int page, int pageSize = 50)
    {
        page = Math.Max(1, page);
        var query = _db.MeterRows(runId).ApplyFilter(filter);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(r => r.MeterNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<MeterResultRow>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public Task<List<ServerOption>> GetServerOptionsAsync() =>
        _db.Servers.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new ServerOption
            {
                Id = s.Id,
                Name = s.Name,
                IpAddress = s.IpAddress,
                Group = s.Group != null ? s.Group.Name : null
            })
            .ToListAsync();

    private static bool IsValidBatchNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value[0] == '0')
        {
            return false;
        }

        return value.All(c => c >= '0' && c <= '9');
    }
}