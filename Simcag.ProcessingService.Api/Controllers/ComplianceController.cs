using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.DTOs;
using Simcag.ProcessingService.Application.UseCases.Compliance;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/compliance")]
[Produces("application/json")]
[Authorize]
public sealed class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComplianceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ComplianceDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetComplianceDashboardQuery(), ct);
        return Ok(dto);
    }

    [HttpGet("findings")]
    [ProducesResponseType(typeof(PagedResult<ExpenseComplianceFindingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFindings(
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] Guid? expenseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ListComplianceFindingsQuery(status, severity, expenseId, page, pageSize),
            ct);
        return Ok(result);
    }

    [HttpGet("rules")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceRuleDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Rules(CancellationToken ct)
    {
        var rules = await _mediator.Send(new ListComplianceRulesQuery(), ct);
        return Ok(rules);
    }
}
