
using ClosedXML.Excel;
using MdmBilling.ViewModels;

namespace MdmBilling.Services.Reports;

public class ExcelReportWriter
{
    public byte[] Write(ReportData d)
    {
        using var wb = new XLWorkbook();

        // Summary worksheet
        var summary = wb.AddWorksheet("Summary");

        var r = 1;

        foreach (var (label, value) in d.SummaryLines())
        {
            summary.Cell(r, 1).Value = label;
            summary.Cell(r, 1).Style.Font.Bold = true;
            summary.Cell(r, 2).Value = value;
            r++;
        }

        summary.Columns().AdjustToContents();

        // Meter details worksheet
        WriteMeters(
            wb.AddWorksheet("Meter Details"),
            d,
            d.Rows
        );

        // Attention list worksheet
        WriteMeters(
            wb.AddWorksheet("Attention List"),
            d,
            d.Rows.Where(x => x.NeedsAttention)
        );

        // Server summary worksheet
        WriteServers(
            wb.AddWorksheet("Server Summary"),
            d.Servers
        );

        using var ms = new MemoryStream();

        wb.SaveAs(ms);

        return ms.ToArray();
    }

    private static void Header(
        IXLWorksheet ws,
        params string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            var c = ws.Cell(1, i + 1);

            c.Value = names[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        ws.SheetView.FreezeRows(1);
    }

    private static void WriteMeters(
        IXLWorksheet ws,
        ReportData d,
        IEnumerable<MeterResultRow> rows)
    {
        Header(
            ws,
            "Meter Number",
            "Batch Number",
            "GEPCO Group",
            "Server",
            "Online / Offline",
            "Daily Data",
            "Missing-data details",
            "Remarks"
        );

        var r = 2;

        foreach (var x in rows)
        {
            // Write meter numbers as text to preserve leading zeros.
            ws.Cell(r, 1).Value = x.MeterNumber.ToString();

            ws.Cell(r, 2).Value = d.BatchNumber;
            ws.Cell(r, 3).Value = x.GroupName ?? "";
            ws.Cell(r, 4).Value = x.ServerName ?? "";
            ws.Cell(r, 5).Value = x.OnlineText;
            ws.Cell(r, 6).Value = x.DataText;
            ws.Cell(r, 7).Value = x.MissingDetails ?? "";
            ws.Cell(r, 8).Value = x.Remarks ?? "";

            r++;
        }

        if (r > 2)
        {
            ws.Range(1, 1, r - 1, 8).SetAutoFilter();
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteServers(
        IXLWorksheet ws,
        List<ServerSummaryRow> servers)
    {
        Header(
            ws,
            "Group",
            "Server",
            "State",
            "Error",
            "Meters",
            "Online",
            "Offline",
            "Data available",
            "Data missing"
        );

        var r = 2;

        foreach (var s in servers)
        {
            ws.Cell(r, 1).Value = s.Group;
            ws.Cell(r, 2).Value = s.Server;
            ws.Cell(r, 3).Value = s.State;
            ws.Cell(r, 4).Value = s.Error ?? "";
            ws.Cell(r, 5).Value = s.Meters;
            ws.Cell(r, 6).Value = s.Online;
            ws.Cell(r, 7).Value = s.Offline;
            ws.Cell(r, 8).Value = s.DataAvailable;
            ws.Cell(r, 9).Value = s.DataMissing;

            r++;
        }

        ws.Columns().AdjustToContents();
    }
}
