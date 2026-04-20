using System.Threading;
using System.Threading.Tasks;

namespace Simcag.ProcessingService.Application.Interfaces;

public interface IRabbitMqConsumer
{
    Task StartAsync(CancellationToken cancellationToken);
}