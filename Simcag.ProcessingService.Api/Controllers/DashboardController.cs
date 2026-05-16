using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.UseCases.Dashboards;
using Simcag.Shared.Security;

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
        var dto = await _mediator.Send(new GetDashboardSummaryQuery(year), ct);
        return Ok(dto);
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

    /// <summary>Insights operacionais: regras determinísticas + camada de explicabilidade; narração LLM opcional via POST /api/ai/insights/narrative.</summary>
    [HttpGet("insights")]
    public async Task<IActionResult> Insights([FromQuery] bool refresh = false, CancellationToken ct = default)
    {
        var envelope = await _mediator.Send(new GetOperationalInsightsQuery(refresh), ct);
        return Ok(envelope);
    }

    /// <summary>Histórico de snapshots persistidos (metadados) para auditoria temporal.</summary>
    [HttpGet("insights/history")]
    public async Task<IActionResult> InsightHistory([FromQuery] int take = 30, CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetOperationalInsightHistoryQuery(take), ct);
        return Ok(new { take, rows });
    }

    [HttpPost("/api/admin/refresh-dashboard")]
    [Authorize(Roles = SimcagRoles.Admin)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await _mediator.Send(new RefreshDashboardCommand(), ct);
        return Ok(new { refreshed = true, at = DateTime.UtcNow });
    }
}
