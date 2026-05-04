using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using Simcag.ProcessingService.Api.Reports;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.Shared.MultiTenancy;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;

    public ReportsController(IExpenseRepository expenses, ISupplierRepository suppliers, ITenantContext tenant)
    {
        _expenses = expenses;
        _suppliers = suppliers;
        _tenant = tenant;
    }

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
        var (expenses, count) = await _expenses.ListAsync(
            status: null, category: null, supplierId: null,
            from: from, to: to, skip: 0, take: 1000, includePayments: true, ct);
        var totalAmount = expenses.Sum(e => e.TotalAmount);
        var totalPaid = expenses.Sum(e => e.TotalPaid);
        var suppliers = await _suppliers.ListAsync(category: null, ct);

        var data = new ExpenseReportDocument.ReportData(
            _tenant.TenantId, label, from, to, totalAmount, totalPaid, count, suppliers.Count, expenses);
        var doc = new ExpenseReportDocument(data);
        var bytes = doc.GeneratePdf();

        var filename = $"relatorio_{label.ToLowerInvariant().Replace(' ', '_')}_{from:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }
}
