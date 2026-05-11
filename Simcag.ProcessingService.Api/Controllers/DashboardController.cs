using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.UseCases.Dashboards;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>Aggregated KPIs for the SPA dashboard (tenant-scoped read model).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var rows = await _mediator.Send(new GetMonthlyDashboardQuery(y), ct);
        var totalAmount = rows.Sum(r => r.TotalAmount);
        var totalExpenseLines = rows.Sum(r => r.ExpenseCount);
        var supplierIds = new HashSet<Guid>(rows.Select(r => r.SupplierId).Where(id => id != Guid.Empty));
        var outstanding = rows.Sum(r => r.Outstanding);

        // Heuristic "status" bars for UI (0–100); refine when domain rules exist.
        var supplierScore = supplierIds.Count == 0 ? 0 : Math.Min(100, supplierIds.Count * 8);
        var docScore = totalExpenseLines == 0 ? 0 : Math.Min(100, 50 + totalExpenseLines);
        var confScore = totalAmount <= 0
            ? 60
            : (int)Math.Clamp(100.0 - (double)(outstanding / totalAmount * 30m), 0, 100);

        return Ok(new
        {
            year = y,
            economiaIdentificada = outstanding > 0 ? outstanding : totalAmount,
            auditoriasRealizadas = totalExpenseLines,
            fornecedoresCadastrados = supplierIds.Count,
            alertasAtivos = 0,
            statusGeral = new
            {
                conformidades = $"{confScore}%",
                documentacao = $"{docScore}%",
                fornecedoresValidados = $"{supplierScore}%"
            }
        });
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var rows = await _mediator.Send(new GetMonthlyDashboardQuery(y), ct);
        return Ok(new { year = y, rows });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.AddMonths(-12);
        var t = to ?? DateTime.UtcNow;
        var rows = await _mediator.Send(new GetCategoryBreakdownQuery(f, t), ct);
        return Ok(new { from = f, to = t, rows });
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.AddMonths(-6);
        var t = to ?? DateTime.UtcNow.AddMonths(3);
        var rows = await _mediator.Send(new GetCashFlowQuery(f, t), ct);
        return Ok(new { from = f, to = t, rows });
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers(
        [FromQuery] int top = 10,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var f = from ?? DateTime.UtcNow.AddMonths(-12);
        var t = to ?? DateTime.UtcNow;
        var rows = await _mediator.Send(new GetSupplierRankingQuery(f, t, top), ct);
        return Ok(new { top, from = f, to = t, rows });
    }

    [HttpGet("year-over-year")]
    public async Task<IActionResult> YearOverYear([FromQuery] int yearsBack = 2, CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetYearOverYearQuery(yearsBack), ct);
        return Ok(new { yearsBack, rows });
    }

    [HttpPost("/api/admin/refresh-dashboard")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await _mediator.Send(new RefreshDashboardCommand(), ct);
        return Ok(new { refreshed = true, at = DateTime.UtcNow });
    }
}
