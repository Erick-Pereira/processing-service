using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseRepository _expenses;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseRepository expenses, ILogger<ExpensesController> logger)
    {
        _expenses = expenses;
        _logger = logger;
    }

    private Guid? ResolveCondominioId() =>
        Request.Headers.TryGetValue("X-Tenant-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category,
        [FromQuery] Guid? supplierId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (page - 1) * pageSize;

        var items = await _expenses.ListAsync(condominioId.Value, from, to, category, supplierId, skip, pageSize, ct);
        var total = await _expenses.CountAsync(condominioId.Value, from, to, category, supplierId, ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            items = items.Select(ToDto)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        var expense = await _expenses.GetByIdAsync(id, condominioId.Value, ct);
        return expense is null ? NotFound() : Ok(ToDto(expense));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category,
        CancellationToken ct = default)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        var total = await _expenses.SumAmountAsync(condominioId.Value, from, to, category, ct);
        var count = await _expenses.CountAsync(condominioId.Value, from, to, category, null, ct);

        return Ok(new { total, count, average = count == 0 ? 0 : total / count });
    }

    private static object ToDto(Expense e) => new
    {
        id = e.Id,
        condominioId = e.CondominioId,
        rawDocumentId = e.RawDocumentId,
        supplierId = e.SupplierId,
        category = e.Category,
        amount = e.Amount,
        currency = e.Currency,
        date = e.Date,
        region = e.Region,
        confidenceScore = e.ConfidenceScore,
        lowConfidence = e.LowConfidence,
        createdAt = e.CreatedAt
    };
}
