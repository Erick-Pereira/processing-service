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
