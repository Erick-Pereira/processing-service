using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.ProcessingService.Application.UseCases.Products;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] Guid? supplierId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int maxSourceRows = 5000,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ListProductCatalogQuery(query, category, supplierId, from, to, page, pageSize, maxSourceRows),
            ct);
        return Ok(result);
    }
}
