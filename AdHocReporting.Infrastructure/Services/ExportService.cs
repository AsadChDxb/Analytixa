using AdHocReporting.Application.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AdHocReporting.Infrastructure.Services;

public sealed class ExportService : IExportService
{
    private readonly IReportService _reportService;

    public ExportService(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<byte[]> ExportReportToPdfAsync(long userId, long reportId, Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var result = await _reportService.RunReportAsync(userId, new Application.DTOs.Reports.ReportExecutionRequest(reportId, runtimeParameters, 1, 5000), cancellationToken);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4.Landscape());
                page.Header().Text($"Report #{reportId}").Bold().FontSize(16);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in result.Columns)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    foreach (var column in result.Columns)
                    {
                        table.Cell().Element(CellStyle).Text(column).Bold();
                    }

                    foreach (var row in result.Rows)
                    {
                        foreach (var column in result.Columns)
                        {
                            row.TryGetValue(column, out var value);
                            table.Cell().Element(CellStyle).Text(value?.ToString() ?? string.Empty);
                        }
                    }
                });
                page.Footer().AlignRight().Text($"Generated {DateTime.UtcNow:u}");
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportReportToExcelAsync(long userId, long reportId, Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.RunReportAsync(userId, new Application.DTOs.Reports.ReportExecutionRequest(reportId, runtimeParameters, 1, 50000), cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");

        for (var i = 0; i < result.Columns.Count; i++)
        {
            ws.Cell(1, i + 1).Value = result.Columns.ElementAt(i);
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in result.Rows)
        {
            for (var colIndex = 0; colIndex < result.Columns.Count; colIndex++)
            {
                var column = result.Columns.ElementAt(colIndex);
                row.TryGetValue(column, out var value);
                ws.Cell(rowIndex, colIndex + 1).Value = value?.ToString() ?? string.Empty;
            }

            rowIndex++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static IContainer CellStyle(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(3);
}
