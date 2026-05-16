using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.Shared.Events;
using Simcag.Shared.Messaging.Contracts;

namespace Simcag.ProcessingService.Application.Adapters;

/// <summary>
/// Adapta o publisher canônico tipado (<see cref="IEventPublisher{TEvent}"/>)
/// para a interface local da Application (<see cref="IEventPublisher"/>),
/// sem reflexão: o tipo concreto de <typeparamref name="T"/> resolve o publisher no DI.
/// </summary>
public sealed class SharedEventPublisherAdapter : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public SharedEventPublisherAdapter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : BaseEvent
    {
        var publisher = _serviceProvider.GetRequiredService<IEventPublisher<T>>();
        return publisher.PublishAsync(eventMessage, cancellationToken);
    }
}
