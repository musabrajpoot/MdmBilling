using System.ComponentModel.DataAnnotations;

namespace MdmBilling.Models;
public class ServerGroup
{
public int Id { get; set; }
public string Name { get; set; } = "";
public List<Server> Servers { get; set; } = new();
}
public class Server
{
public int Id { get; set; }
[Display(Name = "Group (optional)")]
public int? GroupId { get; set; }
public ServerGroup? Group { get; set; }
public string Name { get; set; } = "";
public string IpAddress { get; set; } = "";
public bool IsActive { get; set; } = true;
public List<DatabaseConnection> Connections { get; set; } = new();
}

public class DatabaseConnection
{
public int Id { get; set; }
public int ServerId { get; set; }
public Server? Server { get; set; }
public string DbEngine { get; set; } = "";
public string Host { get; set; } = "";
public int Port { get; set; }
public string DatabaseName { get; set; } = "";
public string Username { get; set; } = "";
public string EncryptedPassword { get; set; } = "";
public bool IsActive { get; set; } = true;
}
public class BillingBatch
{
public int Id { get; set; }
public string BatchNumber { get; set; } = "";
public DateTime? BillingDate { get; set; }
public string CreatedById { get; set; } = "";
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
public List<BatchMeter> Meters { get; set; } = new();
public List<BatchProcessingRun> Runs { get; set; } = new();
public List<BatchServer> BatchServers { get; set; } = new();
}
public class UploadedBatchFile
{
public int Id { get; set; }
public int BatchId { get; set; }
public BillingBatch? Batch { get; set; }
public string FileName { get; set; } = "";
public string StoredPath { get; set; } = "";
public string FileHash { get; set; } = "";
public string UploadedById { get; set; } = "";
public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
public string ValidationStatus { get; set; } = "";
}
public class BatchMeter
{
public int Id { get; set; }
public int BatchId { get; set; }

public BillingBatch? Batch { get; set; }
public int FileId { get; set; }
public UploadedBatchFile? File { get; set; }
public string MeterNumber { get; set; } = "";
}


public class BatchProcessingRun
{
public int Id { get; set; }
public int BatchId { get; set; }
public BillingBatch? Batch { get; set; }
public string StartedById { get; set; } = "";
public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
public DateTime? StartedAt { get; set; }
public DateTime? EndedAt { get; set; }
public RunStatus Status { get; set; } = RunStatus.Queued;
public int TotalMeters { get; set; }
public int OnlineCount { get; set; }
public int OfflineCount { get; set; }
public int DataAvailableCount { get; set; }
public int DataMissingCount { get; set; }
public int NotFoundCount { get; set; }
public int NotCheckedCount { get; set; }
public int ServersTotal { get; set; }
public int ServersDone { get; set; }
public int ServersFailed { get; set; }
}
public class MeterServerMapping
{
public int Id { get; set; }
public int RunId { get; set; }
public BatchProcessingRun? Run { get; set; }
public int BatchMeterId { get; set; }
public BatchMeter? BatchMeter { get; set; }
public int? ServerId { get; set; }
public Server? Server { get; set; }
public bool Found { get; set; }
}
public class MeterStatusResult
{
public int Id { get; set; }

public int RunId { get; set; }
public BatchProcessingRun? Run { get; set; }
public int BatchMeterId { get; set; }
public BatchMeter? BatchMeter { get; set; }
public OnlineStatus OnlineStatus { get; set; }
public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
public string? Remarks { get; set; }
}
public class DailyDataAvailability
{
public int Id { get; set; }
public int RunId { get; set; }
public BatchProcessingRun? Run { get; set; }
public int BatchMeterId { get; set; }
public BatchMeter? BatchMeter { get; set; }
public DataStatus DataStatus { get; set; }
public string? MissingDetails { get; set; }
public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class ServerRunStatus
{
public int RunId { get; set; }
public BatchProcessingRun? Run { get; set; }
public int ServerId { get; set; }
public Server? Server { get; set; }
public ServerRunState State { get; set; } = ServerRunState.Pending;
public string? ErrorMessage { get; set; }
public int DurationMs { get; set; }
}
public class GeneratedReport
{
public int Id { get; set; }
public int RunId { get; set; }
public BatchProcessingRun? Run { get; set; }
public ReportFormat Format { get; set; }
public string StoredPath { get; set; } = "";
public string GeneratedById { get; set; } = "";
public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
public int Id { get; set; }
public string? UserId { get; set; }
public string Action { get; set; } = "";
public string? EntityName { get; set; }
public string? EntityId { get; set; }
public string? IpAddress { get; set; }
public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
public class ErrorLog
{
public int Id { get; set; }
public int? RunId { get; set; }
public int? ServerId { get; set; }
public string Message { get; set; } = "";
public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BatchServer
{
    public int BatchId { get; set; }
    public BillingBatch? Batch { get; set; }
    public int ServerId { get; set; }
    public Server? Server { get; set; }
}
