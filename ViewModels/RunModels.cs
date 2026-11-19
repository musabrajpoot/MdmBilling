using MdmBilling.Models;
namespace MdmBilling.ViewModels;

public class RunProgress
{
    public int RunId { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime? BillingDate { get; set; }
    public RunStatus Status { get; set; }
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
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsFinished => Status is RunStatus.Completed or
    RunStatus.CompletedWithErrors
    or RunStatus.Failed or RunStatus.Interrupted;
    public int Percent => IsFinished ? 100 : ServersTotal == 0 ? 0 :
    ServersDone * 100 / ServersTotal;
}
public class MeterResultRow
{
    public string MeterNumber { get; set; } = "";
    public string? GroupName { get; set; }
    public string? ServerName { get; set; }
    public bool Found { get; set; }
    public OnlineStatus Online { get; set; }
    public DataStatus Data { get; set; }
    public string? MissingDetails { get; set; }
    public string? Remarks { get; set; }
    public bool NeedsAttention => !(Found && Online == OnlineStatus.Online &&
    Data == DataStatus.Available);
    public string OnlineText => !Found && Online == OnlineStatus.Unknown ?
    "Not found" : Online switch
    {
        OnlineStatus.Online => "Online",
        OnlineStatus.Offline => "Offline",
        OnlineStatus.NotChecked => "Not checked",
        _ => "Unknown"
    };
    public string DataText => Data switch
    {
        DataStatus.Available => "Available",
        DataStatus.Missing => "Missing",
        DataStatus.NotChecked => "Not checked",
        _ => "Unknown"
    };
}
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount /
    (double)PageSize));
}
public class RunViewModel
{
    public RunProgress Progress { get; set; } = null!;
    public PagedResult<MeterResultRow>? Results { get; set; }
}
