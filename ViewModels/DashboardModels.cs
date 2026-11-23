using MdmBilling.Models;

namespace MdmBilling.ViewModels;

public class DashboardViewModel
{
    public int TotalBatches { get; set; }
    public int ActiveRuns { get; set; }
    public int RunsLast7Days { get; set; }
    public int FailedRunsLast7Days { get; set; }
    public int RefreshSeconds { get; set; } = 30;
    public bool UsesFakeData { get; set; }
    public RecentRunRow? LatestRun { get; set; }
    public List<RecentRunRow> RecentRuns { get; set; } = new();
    public List<DashboardErrorRow> RecentErrors { get; set; } = new();
}

public class RecentRunRow
{
    public int RunId { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = "";
    public RunStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalMeters { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public int DataAvailableCount { get; set; }
    public int DataMissingCount { get; set; }
    public int NotFoundCount { get; set; }
    public int NotCheckedCount { get; set; }
    public int ServersTotal { get; set; }
    public int ServersFailed { get; set; }
}

public class DashboardErrorRow
{
    public DateTime Time { get; set; }
    public int? RunId { get; set; }
    public string? Server { get; set; }
    public string Message { get; set; } = "";
}

// ---- Server health (sent to the page as JSON) ----

public class ServerHealthRow
{
    public int ServerId { get; set; }
    public string Name { get; set; } = "";
    public string? Group { get; set; }
    public string IpAddress { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Engine { get; set; } = "";
    public string Database { get; set; } = "";
    public bool IsActive { get; set; }

    // online, offline, inactive, unknown
    public string Status { get; set; } = "unknown";
    public int? LatencyMs { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? Error { get; set; }

    public string? LastRunState { get; set; }
    public DateTime? LastRunAt { get; set; }
    public int? LastRunDurationMs { get; set; }
    public string? LastRunError { get; set; }
    public int Runs7d { get; set; }
    public int Failed7d { get; set; }
    public int? AvgDurationMs { get; set; }
}

public class ServerHealthResult
{
    public List<ServerHealthRow> Servers { get; set; } = new();
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Inactive { get; set; }
    public int Unknown { get; set; }
    public int? AvgLatencyMs { get; set; }
    public DateTime CheckedAt { get; set; }
}