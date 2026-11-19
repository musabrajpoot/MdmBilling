
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MdmBilling.Services.Reports;

public class PdfReportWriter
{
    static PdfReportWriter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Write(ReportData d)
    {
        var attention = d.Rows
            .Where(r => r.NeedsAttention)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item()
                        .Text($"MDM Batch Billing Report - Batch {d.BatchNumber}")
                        .FontSize(16)
                        .SemiBold();

                    // Report summary
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });

                        foreach (var (label, value) in d.SummaryLines())
                        {
                            t.Cell()
                                .Border(0.5f)
                                .Padding(3)
                                .Text(label);

                            t.Cell()
                                .Border(0.5f)
                                .Padding(3)
                                .Text(value);
                        }
                    });

                    // Server summary
                    col.Item()
                        .Text("Server summary")
                        .FontSize(12)
                        .SemiBold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        t.Header(h =>
                        {
                            foreach (var name in new[]
                            {
                                "Group",
                                "Server",
                                "State",
                                "Meters",
                                "Online",
                                "Offline"
                            })
                            {
                                h.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(3)
                                    .Text(name)
                                    .SemiBold();
                            }
                        });

                        foreach (var s in d.Servers)
                        {
                            t.Cell().Padding(3).Text(s.Group);
                            t.Cell().Padding(3).Text(s.Server);
                            t.Cell().Padding(3).Text(s.State);
                            t.Cell().Padding(3).Text(s.Meters.ToString());
                            t.Cell().Padding(3).Text(s.Online.ToString());
                            t.Cell().Padding(3).Text(s.Offline.ToString());
                        }
                    });

                    // Attention list
                    col.Item()
                        .Text($"Meters needing attention ({attention.Count})")
                        .FontSize(12)
                        .SemiBold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(3);
                        });

                        t.Header(h =>
                        {
                            foreach (var name in new[]
                            {
                                "Meter",
                                "Server",
                                "Online",
                                "Daily data",
                                "Remarks"
                            })
                            {
                                h.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(3)
                                    .Text(name)
                                    .SemiBold();
                            }
                        });

                        foreach (var r in attention)
                        {
                            t.Cell().Padding(3).Text(r.MeterNumber);
                            t.Cell().Padding(3).Text(r.ServerName ?? "");
                            t.Cell().Padding(3).Text(r.OnlineText);
                            t.Cell().Padding(3).Text(r.DataText);

                            t.Cell()
                                .Padding(3)
                                .Text(r.MissingDetails ?? r.Remarks ?? "");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
