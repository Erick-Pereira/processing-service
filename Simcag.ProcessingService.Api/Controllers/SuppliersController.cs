using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.UseCases.Suppliers;
using Simcag.Shared.Security;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Produces("application/json")]
[Authorize]
public sealed class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    public SuppliersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? category, CancellationToken ct = default)
    {
        var items = await _mediator.Send(new ListSuppliersQuery(category), ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var supplier = await _mediator.Send(new GetSupplierByIdQuery(id), ct);
        return Ok(supplier);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierCommand cmd, CancellationToken ct)
    {
        var id = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierBody body, CancellationToken ct)
    {
        await _mediator.Send(new UpdateSupplierCommand(
            id, body.Name, body.Document, body.Email, body.Phone, body.Address, body.Category), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeactivateSupplierCommand(id), ct);
        return NoContent();
    }

    /// <summary>Reatribui despesas do fornecedor origem ao destino e desativa a origem (consolidação / merge).</summary>
    [HttpPost("merge")]
    [Authorize(Roles = SimcagRoles.Admin)]
    public async Task<IActionResult> Merge([FromBody] MergeSuppliersBody body, CancellationToken ct)
    {
        await _mediator.Send(new MergeSuppliersCommand(body.SourceSupplierId, body.TargetSupplierId), ct);
        return NoContent();
    }
}

public sealed record UpdateSupplierBody(
    string Name,
    string Document,
    string? Email,
    string? Phone,
    string? Address,
    string? Category);

public sealed record MergeSuppliersBody(Guid SourceSupplierId, Guid TargetSupplierId);
