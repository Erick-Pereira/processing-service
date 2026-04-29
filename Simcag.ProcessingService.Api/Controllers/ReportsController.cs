using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using Simcag.ProcessingService.Api.Reports;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;

    public ReportsController(IExpenseRepository expenses, ISupplierRepository suppliers)
    {
        _expenses = expenses;
        _suppliers = suppliers;
    }

    private Guid? ResolveCondominioId() =>
        Request.Headers.TryGetValue("X-Tenant-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;

    [HttpGet("monthly")]
    public Task<IActionResult> Monthly([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var from = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1).AddSeconds(-1);
        return GenerateAsync("Mensal", from, to, ct);
    }

    [HttpGet("quarterly")]
    public Task<IActionResult> Quarterly([FromQuery] int? year, [FromQuery] int? quarter, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var q = quarter ?? ((now.Month - 1) / 3 + 1);
        if (q < 1 || q > 4) return Task.FromResult<IActionResult>(BadRequest(new { error = "Quarter deve ser 1..4" }));
        var startMonth = (q - 1) * 3 + 1;
        var from = new DateTime(y, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(3).AddSeconds(-1);
        return GenerateAsync($"Trimestral Q{q}", from, to, ct);
    }

    [HttpGet("annual")]
    public Task<IActionResult> Annual([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var from = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1).AddSeconds(-1);
        return GenerateAsync($"Anual {y}", from, to, ct);
    }

    private async Task<IActionResult> GenerateAsync(string label, DateTime from, DateTime to, CancellationToken ct)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        var expenses = await _expenses.ListAsync(condominioId.Value, from, to, null, null, 0, 1000, ct);
        var totalAmount = await _expenses.SumAmountAsync(condominioId.Value, from, to, null, ct);
        var count = await _expenses.CountAsync(condominioId.Value, from, to, null, null, ct);
        var suppliers = await _suppliers.ListAsync(condominioId.Value, null, ct);

        var data = new ExpenseReportDocument.ReportData(
            condominioId.Value, label, from, to, totalAmount, count, suppliers.Count, expenses);
        var doc = new ExpenseReportDocument(data);
        var bytes = doc.GeneratePdf();

        var filename = $"relatorio_{label.ToLowerInvariant().Replace(' ', '_')}_{from:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }
}
