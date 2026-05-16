using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.UseCases.Compliance;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Produces("application/json")]
[Authorize]
public sealed class ExpenseComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpenseComplianceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}/compliance")]
    [ProducesResponseType(typeof(ExpenseComplianceSnapshotDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompliance(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetExpenseComplianceSnapshotQuery(id), ct);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/compliance/reevaluate")]
    [ProducesResponseType(typeof(ExpenseComplianceSnapshotDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reevaluate(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new ReevaluateExpenseComplianceCommand(id), ct);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/compliance/findings/{findingId:guid}/waive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Waive(Guid id, Guid findingId, [FromBody] WaiveComplianceBody body, CancellationToken ct)
    {
        await _mediator.Send(new WaiveExpenseComplianceFindingCommand(id, findingId, body.Reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/compliance/findings/{findingId:guid}/comments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddComment(Guid id, Guid findingId, [FromBody] ComplianceCommentBody body, CancellationToken ct)
    {
        await _mediator.Send(new AddExpenseComplianceCommentCommand(id, findingId, body.Body), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/compliance/findings/{findingId:guid}/evidence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetEvidence(Guid id, Guid findingId, [FromBody] ComplianceEvidenceBody body, CancellationToken ct)
    {
        await _mediator.Send(new SetExpenseComplianceEvidenceCommand(id, findingId, body.DocumentIds), ct);
        return NoContent();
    }
}

public sealed record WaiveComplianceBody(string Reason);

public sealed record ComplianceCommentBody(string Body);

public sealed record ComplianceEvidenceBody(IReadOnlyList<Guid> DocumentIds);
