using Simcag.IngestionService.Domain.Events;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Interfaces
{
    public interface IProcessingService
    {
        Task ProcessPriceCollectedEventAsync(PriceCollectedEvent priceEvent, CancellationToken cancellationToken = default);
    }
}