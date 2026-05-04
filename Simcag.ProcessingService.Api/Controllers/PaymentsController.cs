using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentRepository _payments;
    public PaymentsController(IPaymentRepository payments) => _payments = payments;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? expenseId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? refunded,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var p = Math.Max(1, page);
        var s = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _payments.ListAsync(expenseId, from, to, refunded, (p - 1) * s, s, ct);
        return Ok(new
        {
            items = items.Select(x => new
            {
                id = x.Id,
                expenseId = x.ExpenseId,
                amount = x.Amount,
                paymentDate = x.PaymentDate,
                method = x.Method.ToString(),
                referenceCode = x.ReferenceCode,
                isRefunded = x.IsRefunded,
                refundedAt = x.RefundedAt,
            }),
            total,
            page = p,
            pageSize = s,
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var p = await _payments.GetByIdAsync(id, ct);
        return p is null ? NotFound() : Ok(new
        {
            id = p.Id,
            expenseId = p.ExpenseId,
            amount = p.Amount,
            paymentDate = p.PaymentDate,
            method = p.Method.ToString(),
            referenceCode = p.ReferenceCode,
            isRefunded = p.IsRefunded,
            refundedAt = p.RefundedAt,
            refundReason = p.RefundReason,
            createdAt = p.CreatedAt,
        });
    }
}
