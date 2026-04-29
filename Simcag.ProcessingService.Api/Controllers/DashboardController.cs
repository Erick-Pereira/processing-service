using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly IExpenseRepository _expenses;
    private readonly ISupplierRepository _suppliers;

    public DashboardController(IExpenseRepository expenses, ISupplierRepository suppliers)
    {
        _expenses = expenses;
        _suppliers = suppliers;
    }

    private Guid? ResolveCondominioId() =>
        Request.Headers.TryGetValue("X-Tenant-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;

        var totalAmount = await _expenses.SumAmountAsync(condominioId.Value, fromDate, toDate, null, ct);
        var totalExpenses = await _expenses.CountAsync(condominioId.Value, fromDate, toDate, null, null, ct);
        var suppliers = await _suppliers.ListAsync(condominioId.Value, null, ct);

        return Ok(new
        {
            condominioId = condominioId.Value,
            period = new { from = fromDate, to = toDate },
            totalAmount,
            totalExpenses,
            suppliersCount = suppliers.Count,
            averageExpense = totalExpenses == 0 ? 0 : totalAmount / totalExpenses
        });
    }
}
