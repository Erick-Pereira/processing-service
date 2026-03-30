using shared.Events;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IProcessingService
{
    Task ProcessPriceEventAsync(PriceCollectedEvent priceEvent);
}