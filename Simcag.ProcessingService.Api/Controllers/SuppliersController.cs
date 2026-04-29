using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.ProcessingService.Domain.Entities;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _suppliers;

    public SuppliersController(ISupplierRepository suppliers)
    {
        _suppliers = suppliers;
    }

    private Guid? ResolveCondominioId() =>
        Request.Headers.TryGetValue("X-Tenant-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? category, CancellationToken ct = default)
    {
        var condominioId = ResolveCondominioId();
        if (condominioId is null) return BadRequest(new { error = "X-Tenant-Id header obrigatório" });

        var items = await _suppliers.ListAsync(condominioId.Value, category, ct);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var s = await _suppliers.GetByIdAsync(id, ct);
        return s is null ? NotFound() : Ok(ToDto(s));
    }

    private static object ToDto(Supplier s) => new
    {
        id = s.Id,
        condominioId = s.CondominioId,
        cnpj = s.Cnpj,
        normalizedName = s.NormalizedName,
        category = s.Category,
        isActive = s.IsActive,
        createdAt = s.CreatedAt
    };
}
