using AdHocReporting.API.Extensions;
using AdHocReporting.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdHocReporting.API.Controllers;

[ApiController]
[Authorize(Policy = "ExportReport")]
[Route("api/exports")]
public sealed class ExportsController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportsController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpPost("pdf/{reportId:long}")]
    public async Task<IActionResult> Pdf(long reportId, [FromBody] Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportReportToPdfAsync(User.GetUserId(), reportId, runtimeParameters, cancellationToken);
        return File(bytes, "application/pdf", $"report_{reportId}.pdf");
    }

    [HttpPost("excel/{reportId:long}")]
    public async Task<IActionResult> Excel(long reportId, [FromBody] Dictionary<string, object?> runtimeParameters, CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportReportToExcelAsync(User.GetUserId(), reportId, runtimeParameters, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report_{reportId}.xlsx");
    }
}
