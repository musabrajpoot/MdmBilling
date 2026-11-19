using System.ComponentModel.DataAnnotations;
using MdmBilling.Models;

namespace MdmBilling.ViewModels;

public class ExcelReadResult
{
    public List<string> BatchNumbers { get; } = new();

    public List<string> Meters { get; } = new();

    public List<string> Errors { get; } = new();

    public List<string> Warnings { get; } = new();
}

public class UploadResult
{
    public int BatchId { get; set; }

    public int MeterCount { get; set; }

    public List<string> Errors { get; } = new();

    public List<string> Warnings { get; } = new();
}

public class UploadBatchViewModel
{
    [Required(ErrorMessage = "Batch number is required.")]
    [RegularExpression(
        @"^[1-9][0-9]*$",
        ErrorMessage = "Batch number must contain digits only, such as 1, 2, or 3.")]
    [StringLength(50)]
    [Display(Name = "Batch number")]
    public string BatchNumber { get; set; } = "";

    [DataType(DataType.Date)]
    [Display(Name = "Billing date")]
    public DateTime? BillingDate { get; set; }

    [Display(Name = "Excel file (.xlsx)")]
    public IFormFile? File { get; set; }

    public List<string> Errors { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public List<int> SelectedServerIds { get; set; } = new();
    public List<ServerOption> Servers { get; set; } = new();
}

public class BatchListItem
{
    public int Id { get; set; }

    public string BatchNumber { get; set; } = "";

    public DateTime? BillingDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int MeterCount { get; set; }

    public int? LatestRunId { get; set; }

    public RunStatus? LatestRunStatus { get; set; }
}

public class RunListItem
{
    public int Id { get; set; }

    public DateTime QueuedAt { get; set; }

    public RunStatus Status { get; set; }

    public int TotalMeters { get; set; }

    public int OnlineCount { get; set; }

    public int OfflineCount { get; set; }

    public int DataAvailableCount { get; set; }

    public int DataMissingCount { get; set; }
}

public class BatchDetails
{
    public int Id { get; set; }

    public string BatchNumber { get; set; } = "";

    public DateTime? BillingDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int MeterCount { get; set; }

    public string? FileName { get; set; }
    public List<string> ServerNames { get; set; } = new();

    public List<RunListItem> Runs { get; set; } = new();
    public List<ServerOption> AllServers { get; set; } = new();
    public List<int> SelectedServerIds { get; set; } = new();
}

public class ServerOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string? Group { get; set; }
}