using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishProcessedDataAsync(Guid id, string name, string normalizedName, decimal price);
}