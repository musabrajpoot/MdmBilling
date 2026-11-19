
using MdmBilling.ViewModels;

namespace MdmBilling.Services.Reports;

public class ServerSummaryRow
{
    public string Group { get; set; } = "";
    public string Server { get; set; } = "";
    public string State { get; set; } = "";
    public string? Error { get; set; }
    public int Meters { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int DataAvailable { get; set; }
    public int DataMissing { get; set; }
}

public class ReportData
{
    public string BatchNumber { get; set; } = "";
    public DateTime? BillingDate { get; set; }
    public int RunId { get; set; }
    public DateTime RunDate { get; set; }
    public string OverallStatus { get; set; } = "";

    public int TotalMeters { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int DataAvailable { get; set; }
    public int DataMissing { get; set; }
    public int NotFound { get; set; }
    public int NotChecked { get; set; }

    public List<MeterResultRow> Rows { get; set; } = new();
    public List<ServerSummaryRow> Servers { get; set; } = new();

    public List<(string Label, string Value)> SummaryLines() => new()
    {
        ("Batch number", BatchNumber),
        ("Billing date", BillingDate?.ToString("yyyy-MM-dd") ?? ""),
        ("Run date", RunDate.ToString("yyyy-MM-dd HH:mm")),
        ("Overall status", OverallStatus),
        ("Total meters", TotalMeters.ToString()),
        ("Online meters", Online.ToString()),
        ("Offline meters", Offline.ToString()),
        ("Meters with daily data available", DataAvailable.ToString()),
        ("Meters with missing daily data", DataMissing.ToString()),
        ("Meters not found", NotFound.ToString()),
        ("Meters not checked", NotChecked.ToString())
    };
}
