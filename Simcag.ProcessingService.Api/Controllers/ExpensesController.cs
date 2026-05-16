using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.UseCases.Expenses;
using Simcag.ProcessingService.Application.UseCases.Payments;
using Simcag.ProcessingService.Domain.Enums;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Produces("application/json")]
[Authorize]
public sealed class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;
    public ExpensesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ExpenseStatus? status,
        [FromQuery] ExpenseProcessingStatus? processingStatus,
        [FromQuery] ExpenseApprovalStatus? approvalStatus,
        [FromQuery] string? category,
        [FromQuery] Guid? supplierId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ListExpensesQuery(
                status,
                processingStatus,
                approvalStatus,
                category,
                supplierId,
                from,
                to,
                page,
                pageSize),
            ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCommand cmd, CancellationToken ct)
    {
        var id = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new ApproveExpenseCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectExpenseRequest body, CancellationToken ct)
    {
        await _mediator.Send(new RejectExpenseCommand(id, body.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/retry-processing")]
    public async Task<IActionResult> RetryProcessing(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RetryExpenseProcessingCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelExpenseRequest body, CancellationToken ct)
    {
        await _mediator.Send(new CancelExpenseCommand(id, body.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RegisterPayment(Guid id, [FromBody] RegisterPaymentBody body, CancellationToken ct)
    {
        var paymentId = await _mediator.Send(
            new RegisterPaymentCommand(id, body.Amount, body.PaymentDate, body.Method, body.ReferenceCode), ct);
        return Ok(new { paymentId });
    }

    [HttpPost("{id:guid}/payments/{paymentId:guid}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id, Guid paymentId, [FromBody] RefundPaymentBody body, CancellationToken ct)
    {
        await _mediator.Send(new RefundPaymentCommand(id, paymentId, body.Reason), ct);
        return NoContent();
    }
}

public sealed record CancelExpenseRequest(string Reason);

public sealed record RejectExpenseRequest(string Reason);

public sealed record RegisterPaymentBody(decimal Amount, DateTime PaymentDate, PaymentMethod Method, string? ReferenceCode);

public sealed record RefundPaymentBody(string Reason);
