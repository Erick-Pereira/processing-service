using System;
using System.Threading;
using System.Threading.Tasks;
using Simcag.ProcessingService.Application.Interfaces;
using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Application.Adapters;

/// <summary>
/// Adapta o publisher canônico tipado (Simcag.Shared.Messaging.Contracts.IEventPublisher&lt;T&gt;)
/// para a interface local da Application (Simcag.ProcessingService.Application.Interfaces.IEventPublisher),
/// resolvendo o publisher concreto em runtime para evitar acoplamento de transporte.
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
        var publisherType = typeof(Simcag.Shared.Messaging.Contracts.IEventPublisher<>).MakeGenericType(typeof(T));
        var publisher = _serviceProvider.GetService(publisherType)
            ?? throw new InvalidOperationException(
                $"Nenhum IEventPublisher<{typeof(T).Name}> registrado. " +
                $"Registre via builder.Services.AddRabbitMqEventPublisher<{typeof(T).Name}>(exchange).");
        var method = publisherType.GetMethod("PublishAsync")
            ?? throw new InvalidOperationException("Método PublishAsync não encontrado em IEventPublisher<T>.");
        return (Task)method.Invoke(publisher, new object?[] { eventMessage, cancellationToken })!;
    }
}
