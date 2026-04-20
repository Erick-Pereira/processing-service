using Microsoft.AspNetCore.Mvc;
using Simcag.IngestionService.Domain.Events;
using Simcag.ProcessingService.Application.Interfaces;

namespace Simcag.ProcessingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly IProcessingService _processingService;

    public ProcessingController(IProcessingService processingService)
    {
        _processingService = processingService;
    }

    [HttpPost("process")]
    public async Task<ActionResult> ProcessPriceEvent(
        [FromBody] ProcessPriceRequest request)
    {
        var priceEvent = new PriceCollectedEvent
        {
            ProductId = Guid.NewGuid().ToString(),
            ProductName = request.ProductName,
            Price = request.Price,
            Market = request.Source,
            Source = request.Source
        };

        await _processingService.ProcessPriceCollectedEventAsync(priceEvent);

        return Ok();
    }
}

public class ProcessPriceRequest
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CollectionDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Category { get; set; }
}